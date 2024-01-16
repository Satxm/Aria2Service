using System.ServiceProcess;

namespace Aria2Service
{
    internal static class Program
    {
        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>
        static void Main()
        {
            ServiceBase[] ServicesToRun;
            ServicesToRun = new ServiceBase[]
            {
                new Aria2Service()
            };
            ServiceBase.Run(ServicesToRun);
        }
    }
}
