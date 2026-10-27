global using System.ComponentModel.DataAnnotations;
global using System.Data.Common;
global using System.Diagnostics;
global using System.Diagnostics.Metrics;
global using System.Globalization;
global using System.Linq.Expressions;
global using System.Net;
global using System.Security.Claims;
global using System.Text.Json;
global using System.Text.Json.Serialization;
global using System.Threading.RateLimiting;

global using DemoApi.BuildingBlocks.Caching;
global using DemoApi.BuildingBlocks.Errors;
global using DemoApi.BuildingBlocks.Http;
global using DemoApi.BuildingBlocks.Observability;
global using DemoApi.BuildingBlocks.Persistence;
global using DemoApi.BuildingBlocks.RateLimiting;
global using DemoApi.BuildingBlocks.Security;
global using DemoApi.SharedKernel.Caching;
global using DemoApi.SharedKernel.Data;
global using DemoApi.SharedKernel.Domain;
global using DemoApi.SharedKernel.Results;
global using DemoApi.SharedKernel.Security;
global using DemoApi.SharedKernel.Time;

global using FastEndpoints;
global using FastEndpoints.OpenApi;

global using FluentValidation.Results;

global using Microsoft.AspNetCore.Authentication.JwtBearer;
global using Microsoft.AspNetCore.Builder;
global using Microsoft.AspNetCore.Diagnostics;
global using Microsoft.AspNetCore.Hosting;
global using Microsoft.AspNetCore.Http;
global using Microsoft.AspNetCore.Identity;
global using Microsoft.AspNetCore.Mvc;
global using Microsoft.AspNetCore.RateLimiting;
global using Microsoft.EntityFrameworkCore;
global using Microsoft.EntityFrameworkCore.ChangeTracking;
global using Microsoft.EntityFrameworkCore.Diagnostics;
global using Microsoft.EntityFrameworkCore.Metadata.Builders;
global using Microsoft.EntityFrameworkCore.Storage;
global using Microsoft.Extensions.Caching.Distributed;
global using Microsoft.Extensions.Caching.Hybrid;
global using Microsoft.Extensions.Caching.StackExchangeRedis;
global using Microsoft.Extensions.Configuration;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Diagnostics.HealthChecks;
global using Microsoft.Extensions.Hosting;
global using Microsoft.Extensions.Logging;
global using Microsoft.Extensions.Options;
global using Microsoft.IdentityModel.Tokens;

global using Npgsql;

global using OpenTelemetry.Metrics;
global using OpenTelemetry.Resources;
global using OpenTelemetry.Trace;

global using Polly;
global using Polly.CircuitBreaker;
global using Polly.Retry;
global using Polly.Timeout;

global using Scalar.AspNetCore;

global using StackExchange.Redis;

global using PersonalDataAttribute = DemoApi.SharedKernel.Domain.PersonalDataAttribute;
// Merged namespaces expose the same names; pick the intended type for each.
global using ProblemDetails = Microsoft.AspNetCore.Mvc.ProblemDetails;
global using ValidationContext = System.ComponentModel.DataAnnotations.ValidationContext;
global using ValidationFailure = FluentValidation.Results.ValidationFailure;
global using ValidationResult = System.ComponentModel.DataAnnotations.ValidationResult;