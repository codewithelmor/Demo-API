global using System.Collections.Concurrent;
global using System.Globalization;
global using System.Net;
global using System.Net.Http.Json;
global using System.Text;
global using System.Text.Json.Nodes;

global using Dapper;

global using DemoApi.Application.Common;
global using DemoApi.Domain.Customers;
global using DemoApi.Infrastructure.Persistence;
global using DemoApi.IntegrationTests.Infrastructure;
global using DemoApi.SharedKernel.Time;
global using DemoApi.Tests;

global using DotNet.Testcontainers.Builders;
global using DotNet.Testcontainers.Configurations;
global using DotNet.Testcontainers.Containers;

global using Microsoft.AspNetCore.Hosting;
global using Microsoft.AspNetCore.Identity;
global using Microsoft.AspNetCore.Mvc.Testing;
global using Microsoft.AspNetCore.Routing;
global using Microsoft.AspNetCore.TestHost;
global using Microsoft.EntityFrameworkCore;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.DependencyInjection.Extensions;
global using Microsoft.Extensions.Logging;
global using Microsoft.Extensions.Time.Testing;

global using Npgsql;

global using Respawn;
global using Respawn.Graph;

global using Shouldly;

global using Testcontainers.PostgreSql;

global using Xunit;