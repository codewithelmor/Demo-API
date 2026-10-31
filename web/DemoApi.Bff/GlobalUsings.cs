global using System.Buffers.Text;
global using System.Collections.Concurrent;
global using System.Diagnostics;
global using System.Diagnostics.Metrics;
global using System.Globalization;
global using System.Net;
global using System.Net.Http.Headers;
global using System.Net.Sockets;
global using System.Security.Cryptography;
global using System.Text;
global using System.Text.Json;
global using System.Text.Json.Serialization;
global using System.Text.RegularExpressions;
global using System.Threading.RateLimiting;

global using DemoApi.Bff;
global using DemoApi.Bff.Auth;
global using DemoApi.Bff.Configuration;
global using DemoApi.Bff.Health;
global using DemoApi.Bff.Proxy;
global using DemoApi.Bff.Security;
global using DemoApi.Bff.Session;
global using DemoApi.Bff.Spa;
global using DemoApi.Bff.Upstream;

global using Microsoft.AspNetCore.DataProtection;
global using Microsoft.AspNetCore.DataProtection.KeyManagement;
global using Microsoft.AspNetCore.DataProtection.Repositories;
global using Microsoft.AspNetCore.Diagnostics.HealthChecks;
global using Microsoft.AspNetCore.Http.Features;
global using Microsoft.AspNetCore.RateLimiting;
global using Microsoft.AspNetCore.Routing.Patterns;
global using Microsoft.AspNetCore.StaticFiles;
global using Microsoft.Extensions.Caching.Distributed;
global using Microsoft.Extensions.Caching.Memory;
global using Microsoft.Extensions.Caching.StackExchangeRedis;
global using Microsoft.Extensions.DependencyInjection.Extensions;
global using Microsoft.Extensions.Diagnostics.HealthChecks;
global using Microsoft.Extensions.FileProviders;
global using Microsoft.Extensions.Options;
global using Microsoft.Extensions.Primitives;

global using OpenTelemetry;
global using OpenTelemetry.Context.Propagation;
global using OpenTelemetry.Metrics;
global using OpenTelemetry.Resources;
global using OpenTelemetry.Trace;

global using StackExchange.Redis;

global using Yarp.ReverseProxy.Configuration;
global using Yarp.ReverseProxy.Forwarder;
global using Yarp.ReverseProxy.Model;
global using Yarp.ReverseProxy.Transforms;
global using Yarp.ReverseProxy.Transforms.Builder;