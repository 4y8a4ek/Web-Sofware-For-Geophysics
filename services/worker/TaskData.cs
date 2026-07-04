using Newtonsoft.Json;

namespace Worker
{
    public class TaskData
    {
        [JsonProperty("task_id")]
        public string TaskId { get; set; }

        [JsonProperty("stl_path")]
        public string StlPath { get; set; }

        [JsonProperty("shape")]
        public int[] Shape { get; set; }

        [JsonProperty("factor")]
        public int Factor { get; set; }
    }
}