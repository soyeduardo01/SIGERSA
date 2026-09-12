using SIGERSA.Domain.Exceptions;
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
    public void EnsureQualifiedShouldIgnoreOnConflictUpdateClause()
    {
        const string sql = "INSERT INTO \"SIGERSA\".\"USUARIO\" (id) VALUES (@Id) ON CONFLICT (id) DO UPDATE SET modificado_en = CURRENT_TIMESTAMP;";

        Assert.Equal(sql, SigersaSqlGuard.EnsureQualified(sql));
    }

    [Fact]
    public void EnsureQualifiedShouldIgnoreMultilineOnConflictUpdateClause()
    {
        const string sql = """
            INSERT INTO "SIGERSA"."USUARIO" (id) VALUES (@Id)
            ON CONFLICT (id) DO
                UPDATE SET modificado_en = EXCLUDED.modificado_en;
            """;

        Assert.Equal(sql, SigersaSqlGuard.EnsureQualified(sql));
    }

    [Fact]
    public void EnsureQualifiedShouldAcceptLocalCommonTableExpression()
    {
        const string sql = """
            WITH records AS (SELECT id FROM "SIGERSA"."USUARIO")
            SELECT * FROM records;
            """;

        Assert.Equal(sql, SigersaSqlGuard.EnsureQualified(sql));
    }

    [Fact]
    public void EnsureQualifiedShouldNotTreatFromParameterAsTableKeyword()
    {
        const string sql = "SELECT id FROM \"SIGERSA\".\"USUARIO\" WHERE (@From IS NULL);";

        Assert.Equal(sql, SigersaSqlGuard.EnsureQualified(sql));
    }

    [Fact]
    public void EnsureQualifiedShouldStillRejectUnqualifiedTableInsideCommonTableExpression()
    {
        const string sql = """
            WITH records AS (SELECT id FROM USUARIO)
            SELECT * FROM records;
            """;

        Assert.Throws<InvalidOperationException>(() => SigersaSqlGuard.EnsureQualified(sql));
    }

    [Fact]
    public void EnsureQualifiedShouldAcceptTableValuedFunction()
    {
        const string sql = "SELECT value FROM unnest(@Ids::uuid[]) AS selected(value);";

        Assert.Equal(sql, SigersaSqlGuard.EnsureQualified(sql));
    }

    [Fact]
    public void EnsureQualifiedShouldAcceptParenthesizedSubquery()
    {
        const string sql = "SELECT EXISTS (SELECT 1 FROM \"SIGERSA\".\"USUARIO\");";

        Assert.Equal(sql, SigersaSqlGuard.EnsureQualified(sql));
    }

    [Fact]
    public void EnsureQualifiedShouldStillRejectUnqualifiedTableAfterParenthesizedExpression()
    {
        const string sql = "SELECT EXISTS (SELECT 1 FROM USUARIO);";

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

    [Fact]
    public void EnsureAuditedConcurrencyUpdateShouldAcceptCompleteUpdate()
    {
        const string sql = """
            UPDATE "SIGERSA"."USUARIO"
            SET modificado_en = CURRENT_TIMESTAMP,
                modificado_por = @ModificadoPor,
                version_fila = version_fila + 1
            WHERE id = @Id AND version_fila = @VersionFila;
            """;

        Assert.Equal(sql, SigersaSqlGuard.EnsureAuditedConcurrencyUpdate(sql));
    }

    [Fact]
    public void OptimisticConcurrencyGuardShouldRejectStaleVersion()
    {
        var id = Guid.NewGuid();

        var exception = Assert.Throws<OptimisticConcurrencyException>(() =>
            OptimisticConcurrencyGuard.EnsureSingleRowUpdated(0, id));

        Assert.Equal(id, exception.EntityId);
    }
}
