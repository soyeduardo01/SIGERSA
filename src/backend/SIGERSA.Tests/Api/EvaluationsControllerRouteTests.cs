using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIGERSA.Api.Controllers;

namespace SIGERSA.Tests.Api;

public sealed class EvaluationsControllerRouteTests
{
    [Fact]
    public void WorkflowTransitionsHaveExplicitNonReservedRoutes()
    {
        var expected = new Dictionary<string, string>
        {
            [nameof(EvaluationsController.Submit)] = "evaluations/{evaluationId:guid}/submit",
            [nameof(EvaluationsController.Review)] = "evaluations/{evaluationId:guid}/review",
            [nameof(EvaluationsController.Approve)] = "evaluations/{evaluationId:guid}/approve",
            [nameof(EvaluationsController.Close)] = "evaluations/{evaluationId:guid}/close"
        };

        foreach (var (methodName, template) in expected)
        {
            var method = typeof(EvaluationsController).GetMethod(methodName);
            var route = Assert.Single(method!.GetCustomAttributes(typeof(HttpPostAttribute), false)
                .Cast<HttpPostAttribute>());
            Assert.Equal(template, route.Template);
            Assert.DoesNotContain("{action", route.Template, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Theory]
    [InlineData(nameof(EvaluationsController.Start), "TECNICO_EVALUADOR")]
    [InlineData(nameof(EvaluationsController.Finalize), "TECNICO_EVALUADOR")]
    [InlineData(nameof(EvaluationsController.Submit), "TECNICO_EVALUADOR")]
    [InlineData(nameof(EvaluationsController.Approve), "COORDINADOR")]
    public void WorkflowTransitionsEnforceTheResponsibleRole(string methodName, string roles)
    {
        var method = typeof(EvaluationsController).GetMethod(methodName);
        var authorization = Assert.Single(method!.GetCustomAttributes(typeof(AuthorizeAttribute), false)
            .Cast<AuthorizeAttribute>());

        Assert.Equal(roles, authorization.Roles);
    }
}
