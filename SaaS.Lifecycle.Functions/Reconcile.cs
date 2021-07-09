using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using System;

namespace SaaS.Lifecycle.Functions
{
    public static class Reconcile
    {
        [FunctionName("Reconcile")]
        public static void Run([TimerTrigger("0 */5 * * * *")]TimerInfo myTimer, ILogger log)
        {
            log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");
        } 
    }
}
