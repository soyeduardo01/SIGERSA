using SIGERSA.Infrastructure.Persistence;

namespace SIGERSA.Tests.Infrastructure;

public sealed class SigersaSqlGuardTests
{
    [Fact]
    public void EnsureQualifiedShouldAcceptSigersaTable()
    {
        const string sql = "SELECT usuario_id FROM \"SIGERSA\".\"USUARIO\";";

        Assert.Equal(sql, SigersaSqlGuard.EnsureQualified(sql));
    }

    [Fact]
    public void EnsureQualifiedShouldRejectUnqualifiedTable()
    {
        const string sql = "SELECT usuario_id FROM USUARIO;";

        Assert.Throws<InvalidOperationException>(() => SigersaSqlGuard.EnsureQualified(sql));
    }

    [Fact]
    public void EnsureAuditedConcurrencyUpdateShouldRejectMissingRowVersionPredicate()
    {
        const string sql = """
            UPDATE "SIGERSA"."USUARIO"
            SET modificado_en = CURRENT_TIMESTAMP,
                modificado_por = @ModificadoPor,
                version_fila = version_fila + 1
            WHERE id = @Id;
            """;

        Assert.Throws<InvalidOperationException>(() =>
            SigersaSqlGuard.EnsureAuditedConcurrencyUpdate(sql));
    }
}
