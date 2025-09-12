#nullable enable
using System.Reflection;
using UnityEngine;

namespace UnityPackages.mmzkworks.SimpleHttpServer.Runtime
{
    public sealed class SimpleHttpServerBehaviour : MonoBehaviour
    {
        [SerializeField] private int port = 8080;
        private SimpleHttpServer? server;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            server = new SimpleHttpServer(port);
            server.RegisterControllersFrom(Assembly.GetExecutingAssembly());
            server.Start();
        }

        private void OnDestroy()
        {
            server?.Stop();
            server = null;
        }
    }
}