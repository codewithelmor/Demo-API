global using System.ComponentModel.DataAnnotations;
global using System.Text.Json;

global using DemoApi.Api.Common;
global using DemoApi.Api.Features.AdminUsers;
global using DemoApi.Api.Features.Auth;
global using DemoApi.Api.Features.Customers;
global using DemoApi.Api.Features.Orders;
global using DemoApi.Api.Features.Products;
global using DemoApi.Api.Features.Reports;
global using DemoApi.Application.Common;
global using DemoApi.Application.Features.AdminUsers;
global using DemoApi.Application.Features.AdminUsers.ManageUsers;
global using DemoApi.Application.Features.Auth;
global using DemoApi.Application.Features.Auth.ConfirmEmail;
global using DemoApi.Application.Features.Auth.ForgotPassword;
global using DemoApi.Application.Features.Auth.GetMe;
global using DemoApi.Application.Features.Auth.Login;
global using DemoApi.Application.Features.Auth.Refresh;
global using DemoApi.Application.Features.Auth.Register;
global using DemoApi.Application.Features.Auth.ResetPassword;
global using DemoApi.BuildingBlocks;
global using DemoApi.BuildingBlocks.Persistence;
global using DemoApi.BuildingBlocks.Security;
global using DemoApi.Domain.Customers;
global using DemoApi.Domain.Orders;
global using DemoApi.Domain.Products;
global using DemoApi.SharedKernel.Data;
global using DemoApi.SharedKernel.Domain;
global using DemoApi.SharedKernel.Http;
global using DemoApi.SharedKernel.Paging;
global using DemoApi.SharedKernel.Results;
global using DemoApi.SharedKernel.Security;
global using DemoApi.SharedKernel.Time;
global using DemoApi.Tests;

global using FluentValidation;
global using FluentValidation.Results;

global using Microsoft.Extensions.Time.Testing;

global using NSubstitute;

global using Shouldly;

global using Xunit;

global using ValidationResult = FluentValidation.Results.ValidationResult;