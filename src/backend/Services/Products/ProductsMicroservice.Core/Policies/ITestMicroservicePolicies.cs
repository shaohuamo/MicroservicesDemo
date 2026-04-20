using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Polly;

namespace ProductsMicroservice.Core.Policies
{
    public interface ITestMicroservicePolicies
    {
        IAsyncPolicy<HttpResponseMessage> GetFallbackPolicy();
    }
}
