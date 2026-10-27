global using System.Globalization;
global using System.Linq.Expressions;
global using System.Security.Cryptography;
global using System.Text;
global using System.Text.Json;
global using System.Text.Json.Serialization;

global using Dapper;

global using DemoApi.Application.Common;
global using DemoApi.Application.Features.Orders;
global using DemoApi.Application.Features.Products.Common;
global using DemoApi.Domain.Customers;
global using DemoApi.Domain.Orders;
global using DemoApi.Domain.Products;
global using DemoApi.SharedKernel.Caching;
global using DemoApi.SharedKernel.Data;
global using DemoApi.SharedKernel.Http;
global using DemoApi.SharedKernel.Paging;
global using DemoApi.SharedKernel.Queries;
global using DemoApi.SharedKernel.Results;
global using DemoApi.SharedKernel.Security;
global using DemoApi.SharedKernel.Time;

global using FastEndpoints;

global using Microsoft.EntityFrameworkCore;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Logging;