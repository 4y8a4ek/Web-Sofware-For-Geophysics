using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Confluent.Kafka;
using Newtonsoft.Json;
using StackExchange.Redis;
using MeshDataModel;
using Prometheus;

namespace Worker
{
    class Program
    {
        private static readonly Counter TasksProcessed = Metrics
            .CreateCounter("tasks_processed_total", "Total processed tasks", new CounterConfiguration
            {
                LabelNames = new[] { "status" }
            });
        private static readonly Gauge ActiveTasks = Metrics
            .CreateGauge("active_tasks", "Currently active tasks");
        private static readonly Histogram TaskProcessingDuration = Metrics
            .CreateHistogram("task_processing_duration_seconds", "Processing time per task");

        static async Task Main(string[] args)
        {
            // Запускаем HTTP-сервер для метрик на порту 9000
            var metricServer = new MetricServer(port: 9000);
            metricServer.Start();
            Console.WriteLine("🚀 C# Worker started, metrics on :9000");

            var consumerConfig = new ConsumerConfig
            {
                BootstrapServers = Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP_SERVERS") ?? "kafka:9092",
                GroupId = "worker-group",
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoCommit = false
            };

            var redis = ConnectionMultiplexer.Connect(
                Environment.GetEnvironmentVariable("REDIS_CONNECTION") ?? "redis:6379"
            );
            var db = redis.GetDatabase();

            using var consumer = new ConsumerBuilder<Ignore, string>(consumerConfig).Build();
            consumer.Subscribe("tasks");

            Console.WriteLine("✅ Subscribed to Kafka topic 'tasks'");

            while (true)
            {
                try
                {
                    var result = consumer.Consume();
                    var taskData = JsonConvert.DeserializeObject<TaskData>(result.Message.Value);
                    Console.WriteLine($"📥 Received task: {taskData.TaskId}");

                    using (ActiveTasks.TrackInProgress())
                    using (TaskProcessingDuration.NewTimer())
                    {
                        await db.HashSetAsync($"task:{taskData.TaskId}", "status", "processing");

                        var stlPath = taskData.StlPath;
                        if (!File.Exists(stlPath))
                        {
                            Console.WriteLine($"❌ STL file not found: {stlPath}");
                            await db.HashSetAsync($"task:{taskData.TaskId}", "status", "error");
                            await db.HashSetAsync($"task:{taskData.TaskId}", "error", "STL file not found");
                            TasksProcessed.WithLabels("error").Inc();
                            consumer.Commit(result);
                            continue;
                        }

                        var (vertices, triangles) = ParseStl(stlPath);
                        Console.WriteLine($"✅ Loaded {vertices.Count} vertices, {triangles.Count} triangles");

                        var (points, edges, faces, elements) = MeshBuilder.BuildFromTriangles(vertices, triangles);

                        Console.WriteLine($"📊 Mesh statistics:");
                        Console.WriteLine($"   Points: {points.Count}");
                        Console.WriteLine($"   Edges: {edges.Count}");
                        Console.WriteLine($"   Faces: {faces.Count}");
                        Console.WriteLine($"   Elements (triangles): {elements.Count}");

                        await db.HashSetAsync($"task:{taskData.TaskId}", "stats",
                            $"Points:{points.Count},Edges:{edges.Count},Faces:{faces.Count},Elements:{elements.Count}");
                        await db.HashSetAsync($"task:{taskData.TaskId}", "status", "done");

                        TasksProcessed.WithLabels("success").Inc();
                        Console.WriteLine($"✅ Task {taskData.TaskId} completed");
                        consumer.Commit(result);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error: {ex.Message}");
                    TasksProcessed.WithLabels("error").Inc();
                }
            }
        }

        static (List<Vector3>, List<(int, int, int)>) ParseStl(string filePath)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<(int, int, int)>();

            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            using var reader = new BinaryReader(fs);

            reader.ReadBytes(80);
            uint triangleCount = reader.ReadUInt32();

            for (int i = 0; i < triangleCount; i++)
            {
                reader.ReadSingle(); reader.ReadSingle(); reader.ReadSingle();

                var v0 = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                var v1 = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                var v2 = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());

                int idx0 = vertices.Count;
                vertices.Add(v0);
                int idx1 = vertices.Count;
                vertices.Add(v1);
                int idx2 = vertices.Count;
                vertices.Add(v2);

                triangles.Add((idx0, idx1, idx2));

                reader.ReadUInt16();
            }

            return (vertices, triangles);
        }
    }
}