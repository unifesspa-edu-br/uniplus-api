namespace Unifesspa.UniPlus.Selecao.IntegrationTests.RolDeRegras;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// As guardas da migration que substitui a definição de
/// <c>RECURSO-PRAZO-ANCORADO-EM-ATO</c> no lugar, ao inverter a política de unidade do prazo
/// de interposição.
/// </summary>
/// <remarks>
/// <para>
/// Substituir no lugar é legítimo só enquanto a entrada for vocabulário, não fato
/// (ADR-0112). Duas populações a referenciam, e cada uma pede um tratamento:
/// </para>
/// <para>
/// A <b>congelada</b> — <c>versoes_configuracao</c> — é imutável, então a migration aborta
/// diante dela. A <b>viva</b> — <c>regras_recurso_fase</c> de rascunho — não passa por
/// <c>RegraRecursoFase.Criar</c> ao ser reidratada pelo EF, e nada revalida a unidade no
/// caminho de publicação: um rascunho declarado em dia corrido publicaria com uma unidade
/// que a regra vigente recusa. Por isso a migration aborta nesse caso também, em vez de
/// escolher uma unidade no lugar de quem declarou.
/// </para>
/// <para>
/// A guarda da viva é <b>simétrica</b>, e cada sentido recusa a unidade que a definição de
/// destino não admite: avançar recusa dia corrido, reverter recusa dia útil. Sem a metade da
/// reversão, um rascunho criado sob a política nova sobreviveria à volta da definição que
/// proíbe dia útil na interposição.
/// </para>
/// <para>
/// A validade sintática do SQL não é objeto deste arquivo: a suíte de integração aplica
/// todas as migrations contra um Postgres real ao subir, e um bloco malformado derrubaria o
/// fixture antes de qualquer asserção aqui.
/// </para>
/// </remarks>
public sealed class SubstituicaoDaRegraDeRecursoGuardaTests
{
    private const string ArquivoDaMigration = "20260814222412_SubstituiInvariantesPrazoInterposicaoEmDiaUtil.cs";

    private static string Migration() => FronteiraAppendOnlyDoRol.LerMigration(ArquivoDaMigration);

    [Fact(DisplayName = "A migration aborta diante de configuração congelada que referencia a entrada")]
    public void Up_GuardaAConfiguracaoCongelada()
    {
        string migration = Migration();

        migration.Should().Contain("selecao.versoes_configuracao");
        migration.Should().Contain(
            """@.codigo == "RECURSO-PRAZO-ANCORADO-EM-ATO" && @.versao == "v1" && exists(@.hash)""",
            "a referência é a tripla — procurar só pela chave bare 'codigo' pegaria homônimo, que não é referência");
        migration.Should().Contain("ADR-0112");
    }

    [Fact(DisplayName = "A migration aborta diante de rascunho com prazo de interposição em dias corridos")]
    public void Up_GuardaORascunhoEmDiasCorridos()
    {
        string migration = Migration();

        migration.Should().Contain("selecao.regras_recurso_fase",
            "a regra viva do rascunho referencia a entrada por colunas próprias, e escapa da guarda sobre a configuração congelada");
        // O SQL interpola o parâmetro, então o fonte não traz o literal: o que se prova é
        // que as constantes valem os enums certos e que o avanço passa a de dia corrido.
        migration.Should().Contain($"const int DiasCorridos = {(int)UnidadePrazo.Dias};",
            "apontar para outro valor do enum faria a guarda vigiar uma unidade que continua válida");
        migration.Should().Contain("DiasCorridos,",
            "é a unidade que deixa de ser declarável ao avançar; as demais continuam válidas e não podem bloquear o deploy");
        migration.Should().Contain("redeclare o prazo em dias úteis ou horas",
            "a mensagem precisa dizer o que fazer — converter automaticamente mudaria o prazo ou a granularidade, e essa é decisão de quem declarou");
    }

    [Fact(DisplayName = "As referências vivas que continuam válidas acompanham o hash da definição substituída")]
    public void Up_ReapontaOHashDasReferenciasVivas()
    {
        string migration = Migration();

        migration.Should().Contain("UPDATE selecao.regras_recurso_fase",
            "sem versão sucessora, o hash antigo deixaria de descrever definição alguma do catálogo");
        migration.Should().Contain("SET regra_hash");
    }

    [Fact(DisplayName = "A reversão devolve o hash anterior — a substituição no lugar é simétrica nos dois sentidos")]
    public void Down_DevolveOHashAnterior()
    {
        string down = FronteiraAppendOnlyDoRol.BlocoDown(Migration());

        down.Should().Contain("DevolverHashDasRegrasDeRecursoVivas",
            "voltar a definição sem voltar a referência viva deixaria o rascunho apontando para um hash que não existe mais");
        down.Should().Contain("ExigirQueNenhumaConfiguracaoCongeladaReferencie",
            "a reversão responde à mesma fronteira do avanço");
    }

    [Fact(DisplayName = "A reversão aborta diante de rascunho em dias úteis — o problema espelhado do avanço")]
    public void Down_GuardaORascunhoEmDiasUteis()
    {
        string migration = Migration();

        migration.Should().Contain($"const int DiasUteis = {(int)UnidadePrazo.DiasUteis};");
        migration.Should().Contain("DiasUteis,",
            "um rascunho declarado sob a política nova sobreviveria à volta de uma definição que proíbe dia útil na interposição, e publicaria — nada revalida a unidade ao carregar");
        migration.Should().Contain("redeclare o prazo em horas ou dias corridos",
            "a orientação da reversão é o inverso da do avanço: o que volta a valer é a política antiga");
    }
}
