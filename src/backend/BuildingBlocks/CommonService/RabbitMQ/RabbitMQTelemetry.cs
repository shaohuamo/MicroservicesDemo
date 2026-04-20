using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommonService.RabbitMQ
{
    public static class RabbitMQTelemetry
    {
        public static readonly ActivitySource ActivitySource = new("RabbitMQ");
    }
}
