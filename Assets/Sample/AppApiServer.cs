#nullable enable
#if !UNITY_EDITOR
using System;
#endif
using System.Reflection;
using UnityEngine;

namespace mmzkworks.SimpleHttpServer
{
    public sealed class AppApiServer : MonoBehaviour
    {
        [SerializeField] private int port = 8080;
        private SimpleHttpServer? server;

        private void Awake()
        {
#if UNITY_EDITOR
            var enabledAtStartup = true;
#else
#if DEVELOPMENT_BUILD
            var enabledAtStartup = true;
#else
            var enabledAtStartup = false;
#endif

            var args = Environment.GetCommandLineArgs();
            for (var index = 0; index < args.Length; index++)
            {
                var arg = args[index];
                switch (arg)
                {
                    case "--enableServer":
                        enabledAtStartup = true;
                        break;
                    
                    case "--serverPort":
                        port = int.Parse(args[++index]);
                        break;
                }
            }
#endif

            DontDestroyOnLoad(gameObject);
            server = new SimpleHttpServer(port);
            server.RegisterControllersFrom(Assembly.GetExecutingAssembly());

            if (enabledAtStartup) server.Start();
        }

        private void OnDestroy()
        {
            server?.Stop();
            server = null;
        }
    }
}