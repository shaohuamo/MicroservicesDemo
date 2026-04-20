using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommonService.RabbitMQ
{
    public class RabbitMQOptions
    {
        public string HostName { get; set; } = default!;
        public string UserName { get; set; } = default!;
        public string Password { get; set; } = default!;
        public int Port { get; set; }

        public int MaxRetryAttempts { get; set; } = 10;
        public int InitialDelaySeconds { get; set; } = 2;
        public int MaxDelaySeconds { get; set; } = 30;
        public int NetworkRecoveryIntervalSeconds { get; set; } = 10;
    }
}
