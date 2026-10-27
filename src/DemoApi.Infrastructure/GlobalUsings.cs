global using System.Globalization;
global using System.Security.Claims;
global using System.Security.Cryptography;
global using System.Text;

global using Dapper;

global using DemoApi.Application.Common;
global using DemoApi.BuildingBlocks;
global using DemoApi.BuildingBlocks.Observability;
global using DemoApi.BuildingBlocks.Persistence;
global using DemoApi.BuildingBlocks.Security;
global using DemoApi.Domain.Customers;
global using DemoApi.Domain.Orders;
global using DemoApi.Domain.Products;
global using DemoApi.Infrastructure.Email;
global using DemoApi.Infrastructure.Identity;
global using DemoApi.Infrastructure.Persistence;
global using DemoApi.SharedKernel.Data;
global using DemoApi.SharedKernel.Domain;
global using DemoApi.SharedKernel.Results;
global using DemoApi.SharedKernel.Time;

global using Microsoft.AspNetCore.DataProtection;
global using Microsoft.AspNetCore.Identity;
global using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
global using Microsoft.EntityFrameworkCore;
global using Microsoft.EntityFrameworkCore.Design;
global using Microsoft.EntityFrameworkCore.Metadata.Builders;
global using Microsoft.Extensions.Caching.Memory;
global using Microsoft.Extensions.Configuration;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Hosting;
global using Microsoft.Extensions.Logging;
global using Microsoft.Extensions.Options;
global using Microsoft.IdentityModel.JsonWebTokens;
global using Microsoft.IdentityModel.Tokens;

global using Npgsql;