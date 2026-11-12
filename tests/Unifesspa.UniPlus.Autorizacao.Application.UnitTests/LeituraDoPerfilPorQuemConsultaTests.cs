namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;

using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.CenarioDaGovernancaDaVersao;

/// <summary>
/// Quem só consulta as versões de perfil, como o Encarregado de dados, lê o perfil, o documento, a janela, a contenção, a
/// reclassificação e as divergências, com a leitura registrada pela consulta; as filas de decisão continuam de quem decide
/// a etapa. Cecília Moura só consulta.
/// </summary>
public sealed class LeituraDoPerfilPorQuemConsultaTests
{
    private const string ConsultarVersoes = "autorizacao:perfis:consultar-versoes";

    private static readonly Guid Recurso = Guid.CreateVersion7();

    [Theory(DisplayName = "Quem só consulta as versões lê e a trilha registra a consulta permitida")]
    [InlineData("perfil")]
    [InlineData("documento")]
    [InlineData("contencao")]
    [InlineData("reclassificacao")]
    [InlineData("divergencias")]
    public async Task Leitura_ComSoAConsulta_Permite(string leitura)
    {
        VerificadorDeTeste verificador = new("cecilia.moura", [ConsultarVersoes]);

        AcessoAvaliado acesso = await AvaliarAsync(leitura, verificador);

        acesso.Should().Be(AcessoAvaliado.Permitido, leitura);
        verificador.Gravados.Should().ContainSingle().Subject.Should().ContainSingle().Which.Permissao.Should().Be(ConsultarVersoes);
    }

    [Theory(DisplayName = "Quem só consulta as versões não lista as filas de decisão")]
    [InlineData(EtapaDaVersao.GestaoDeAcessos)]
    [InlineData(EtapaDaVersao.ProtecaoDeDados)]
    public async Task FilaDaEtapa_ComSoAConsulta_Recusa(EtapaDaVersao etapa)
    {
        VerificadorDeTeste verificador = new("cecilia.moura", [ConsultarVersoes]);

        AcessoAvaliado acesso = await AcessoDaVersao.AvaliarListagemAsync(verificador, etapa, CancellationToken.None);

        acesso.Should().Be(AcessoAvaliado.Negado);
    }

    private static Task<AcessoAvaliado> AvaliarAsync(string leitura, VerificadorDeTeste verificador) => leitura switch
    {
        "perfil" => AcessoDaVersao.AvaliarLeituraDoPerfilAsync(verificador, Recurso, CancellationToken.None),
        "documento" => AcessoDaVersao.AvaliarLeituraDoDocumentoAsync(verificador, Recurso, CancellationToken.None),
        "contencao" => AcessoDaVersao.AvaliarLeituraDaContencaoAsync(verificador, Recurso, CancellationToken.None),
        "reclassificacao" => AcessoDaVersao.AvaliarLeituraDaReclassificacaoAsync(verificador, Recurso, CancellationToken.None),
        _ => AcessoDaVersao.AvaliarVisaoDaGovernancaAsync(verificador, CancellationToken.None),
    };
}
