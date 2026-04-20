using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProductsMicroservice.Core.DTO
{
    public class RedisDataWrapper<T>
    {
        public T Data { get; set; } = default!;
        public DateTime LogicExpireTime { get; set; }
    }
}
