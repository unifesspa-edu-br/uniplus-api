namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Sementes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;

/// <summary>
/// O texto do SQL que grava o termo de consentimento e a versão dele: o termo só entra quando nenhum termo, vivo ou
/// removido, tem o mesmo identificador ou o mesmo código, e a versão só entra quando o termo está gravado.
/// Não usa banco: o comportamento sobre banco real está em <see cref="SementeDeTermoRemovidoTests"/>.
/// </summary>
public sealed class SqlDoTermoDaSementeTests
{
    private const string Tabela = "configuracao.termos_de_consentimento";

    private static readonly TermoDaSemente Semente = new(
        Guid.Parse("7e570001-0000-7000-8000-000000000001"), Guid.Parse("7e570002-0000-7000-8000-000000000001"),
        "Termo fictício da semente de teste", "Texto fictício.", "Base legal fictícia.");

    private static readonly DeclaracaoDaSemente Declaracao = new(
        "de teste", "semente-de-teste", new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero));

    private static string[] Comandos() => [.. SqlDaSemente.ComandosDoTermo(Semente, Declaracao).Select(static c => c.Texto)];

    [Fact(DisplayName = "A semente do termo gera um comando para o termo e outro para a versão, nessa ordem")]
    public void Comandos_TermoEDepoisVersao()
    {
        string[] comandos = Comandos();

        comandos.Should().HaveCount(2);
        comandos[0].Should().StartWith("INSERT INTO configuracao.termos_de_consentimento ");
        comandos[1].Should().StartWith("INSERT INTO configuracao.versoes_do_termo_consentimento ");
    }

    [Fact(DisplayName = "O termo só é inserido quando nenhum termo, vivo ou removido, tem o mesmo identificador ou o mesmo código")]
    public void Termo_ExigeQueIdentificadorECodigoNaoExistam()
    {
        Comandos()[0].Should().Contain(
            $"WHERE NOT EXISTS (SELECT 1 FROM {Tabela} WHERE id = '{Semente.TermoId}'::uuid OR codigo = 'TERMO_FICTICIO_DA_SEMENTE_DE_TESTE') ON CONFLICT");
    }

    [Fact(DisplayName = "A condição do termo não olha a exclusão lógica nem o nome")]
    public void Termo_CondicaoNaoOlhaExclusaoLogicaNemNome()
    {
        Comandos()[0].Should().NotContain("is_deleted = TRUE").And.NotContain("nome =");
    }

    [Fact(DisplayName = "Cada comando traz o item que pode pular, com o código do termo formado do nome")]
    public void Comandos_TrazOItemQuePodePular()
    {
        ComandoDaSemente[] comandos = [.. SqlDaSemente.ComandosDoTermo(Semente, Declaracao)];

        comandos[0].Item.Should().Be(new ItemDaSemente(TipoDoItemDaSemente.Termo, Semente.TermoId, "TERMO_FICTICIO_DA_SEMENTE_DE_TESTE"));
        comandos[1].Item.Should().Be(new ItemDaSemente(TipoDoItemDaSemente.VersaoDoTermo, Semente.VersaoId, "TERMO_FICTICIO_DA_SEMENTE_DE_TESTE", Semente.TermoId));
    }

    [Fact(DisplayName = "O termo mantém o ON CONFLICT pelo identificador contra a corrida")]
    public void Termo_MantemOnConflictPeloIdentificador()
    {
        Comandos()[0].Should().EndWith(" ON CONFLICT (id) DO NOTHING;");
    }

    [Fact(DisplayName = "A versão só é inserida quando o termo dela está gravado e não removido, e mantém o ON CONFLICT pelo identificador")]
    public void Versao_ExigeQueOTermoExista()
    {
        string versao = Comandos()[1];

        versao.Should().Contain($"WHERE EXISTS (SELECT 1 FROM {Tabela} WHERE id = '{Semente.TermoId}'::uuid AND is_deleted = FALSE) ");
        versao.Should().EndWith(" ON CONFLICT (id) DO NOTHING;");
    }

    [Fact(DisplayName = "A versão só é inserida quando o termo ainda não tem versão com o mesmo número")]
    public void Versao_ExigeQueONumeroNaoExistaNoTermo()
    {
        Comandos()[1].Should().Contain(
            "AND NOT EXISTS (SELECT 1 FROM configuracao.versoes_do_termo_consentimento "
            + $"WHERE termo_consentimento_id = '{Semente.TermoId}'::uuid AND numero = 1) ON CONFLICT");
    }
}
