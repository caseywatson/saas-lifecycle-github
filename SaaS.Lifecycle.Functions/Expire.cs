using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using System;

namespace SaaS.Lifecycle.Functions
{
    public static class Expire
    {
        // Expires old operations.

        [FunctionName("Expire")]
        public static void Run([TimerTrigger("0 * */1 * * *")]TimerInfo myTimer, ILogger log)
        {
            log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");
        }
    }
}
