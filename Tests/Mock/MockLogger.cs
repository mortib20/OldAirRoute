using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tests.Mock
{
    public static class MockLogger
    {
        public static ILoggerFactory Factory { get; private set; }

        static MockLogger()
        {
            Factory = LoggerFactory.Create(c =>
            {
                c.SetMinimumLevel(LogLevel.Trace);
            });
        }
    }
}
