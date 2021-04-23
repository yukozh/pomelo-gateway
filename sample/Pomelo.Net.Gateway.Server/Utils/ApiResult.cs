using Newtonsoft.Json;
using System.Collections.Generic;

namespace Pomelo.Net.Gateway
{
    public class ApiResult
    {
        public int Code { get; set; } = 200;
        public string Message { get; set; }
    }

    public class ApiResult<T> : ApiResult
    {
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public T Data { get; set; }
    }
    public class ApiPagedResult<T>
    {
        public int Code { get; set; } = 200;

        public string Message { get; set; }

        public int TotalRows { get; set; }

        public int TotalPages { get; set; }

        public int Current { get; set; }

        public int PageSize { get; set; }

        public IEnumerable<T> Data { get; set; }
    }
}
