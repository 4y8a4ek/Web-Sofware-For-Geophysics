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

namespace Worker
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("🚀 C# Worker started");

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

                    // Обновляем статус в Redis
                    await db.HashSetAsync($"task:{taskData.TaskId}", "status", "processing");

                    // 1. Читаем STL
                    var stlPath = taskData.StlPath;
                    if (!File.Exists(stlPath))
                    {
                        Console.WriteLine($"❌ STL file not found: {stlPath}");
                        await db.HashSetAsync($"task:{taskData.TaskId}", "status", "error");
                        await db.HashSetAsync($"task:{taskData.TaskId}", "error", "STL file not found");
                        consumer.Commit(result);
                        continue;
                    }

                    // 2. Парсим STL в вершины и треугольники
                    var (vertices, triangles) = ParseStl(stlPath);
                    Console.WriteLine($"✅ Loaded {vertices.Count} vertices, {triangles.Count} triangles");

                    // 3. Строим структуру данных
                    var (points, edges, faces, elements) = MeshBuilder.BuildFromTriangles(vertices, triangles);

                    // 4. Выводим статистику
                    Console.WriteLine($"📊 Mesh statistics:");
                    Console.WriteLine($"   Points: {points.Count}");
                    Console.WriteLine($"   Edges: {edges.Count}");
                    Console.WriteLine($"   Faces: {faces.Count}");
                    Console.WriteLine($"   Elements (triangles): {elements.Count}");

                    // 5. Сохраняем статистику в Redis (опционально)
                    await db.HashSetAsync($"task:{taskData.TaskId}", "stats",
                        $"Points:{points.Count},Edges:{edges.Count},Faces:{faces.Count},Elements:{elements.Count}");

                    // 6. Обновляем статус в Redis
                    await db.HashSetAsync($"task:{taskData.TaskId}", "status", "done");

                    Console.WriteLine($"✅ Task {taskData.TaskId} completed");
                    consumer.Commit(result);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error: {ex.Message}");
                }
            }
        }

        static (List<Vector3>, List<(int, int, int)>) ParseStl(string filePath)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<(int, int, int)>();

            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            using var reader = new BinaryReader(fs);

            // 80-байтовый заголовок
            reader.ReadBytes(80);
            uint triangleCount = reader.ReadUInt32();

            for (int i = 0; i < triangleCount; i++)
            {
                // Нормаль (пропускаем)
                reader.ReadSingle(); reader.ReadSingle(); reader.ReadSingle();

                // 3 вершины
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

                // 2-байтовый атрибут (пропускаем)
                reader.ReadUInt16();
            }

            return (vertices, triangles);
        }
    }
}