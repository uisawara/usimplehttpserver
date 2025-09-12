#nullable enable
using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace UnityPackages.mmzkworks.SimpleHttpServer.Runtime
{
    [RoutePrefix("/api")]
    public sealed class StateController
    {
        private readonly SystemStateCollector _systemStateCollector = new();

        // GET /api/state
        [HttpGet("/state")]
        public State GetState()
        {
            return _systemStateCollector.GetState();
        }

        // GET /api/state/{key}
        [HttpGet("/state/{key}")]
        public State GetStateByKey(string key)
        {
            return _systemStateCollector.GetState(new[] { key });
        }
    }

    public class SystemStateCollector
    {
        public State GetState(string[] keys = null)
        {
            var state = new State();

            if (keys == null || keys.Contains("environments"))
                state.Environment = new State.EnvironmentInfo(
                    Environment.GetEnvironmentVariables(),
                    Environment.GetCommandLineArgs());
            if (keys == null || keys.Contains("application"))
                state.Application = new State.ApplicationInfo(
                    Application.productName,
                    Application.version);
            if (keys == null || keys.Contains("runtime"))
                state.Runtime = new State.RuntimeInfo(
                    Time.realtimeSinceStartup,
                    Application.targetFrameRate);

            return state;
        }
    }

    public sealed class State
    {
        public ApplicationInfo? Application { get; set; }
        public RuntimeInfo? Runtime { get; set; }
        public EnvironmentInfo Environment { get; set; }

        public sealed class EnvironmentInfo
        {
            public IDictionary EnvironmentVariables { get; }
            public string[] CommandLineArgs { get; }

            public EnvironmentInfo(IDictionary environmentVariables, string[] commandLineArgs)
            {
                EnvironmentVariables = environmentVariables;
                CommandLineArgs = commandLineArgs;
            }
        }
        
        public sealed class ApplicationInfo
        {
            public ApplicationInfo(string name, string version)
            {
                Name = name;
                Version = version;
            }

            public string Name { get; }
            public string Version { get; }
        }

        public sealed class RuntimeInfo
        {
            public RuntimeInfo(float targetFrameRate, float realtimeSinceStartup)
            {
                TargetFrameRate = targetFrameRate;
                RealtimeSinceStartup = realtimeSinceStartup;
            }

            public float TargetFrameRate { get; }
            public float RealtimeSinceStartup { get; }
        }
    }
}