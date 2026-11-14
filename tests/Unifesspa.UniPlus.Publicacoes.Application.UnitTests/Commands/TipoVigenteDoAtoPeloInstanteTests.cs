namespace Unifesspa.UniPlus.Publicacoes.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Application.Commands.AtosNormativos;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Domain.Errors;
using Unifesspa.UniPlus.Publicacoes.Domain.Interfaces;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O tipo de ato vigente é o do dia civil em que o sistema divulga o ato, no fuso institucional: a data que o
/// documento declara não participa da escolha.
/// </summary>
public sealed class TipoVigenteDoAtoPeloInstanteTests
{
    private const string HashValido = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string Tipo = "EDITAL_ABERTURA";
    private static readonly DateOnly DiaDaPublicacaoEmBelem = new(2026, 3, 13);

    private readonly ITipoAtoPublicadoRepository _tipos = Substitute.For<ITipoAtoPublicadoRepository>();
    private readonly IAtoNormativoRepository _atos = Substitute.For<IAtoNormativoRepository>();

    [Fact(DisplayName = "Às 23h30 em Belém, que em UTC já é o dia seguinte, o tipo é o vigente no dia de Belém")]
    public async Task Montar_InstanteQueEmUtcJaEOutroDia_ResolveOTipoPeloDiaDeBelem()
    {
        DateTimeOffset vinteETresEMeiaEmBelem = InstanteEmBelem.Em(2026, 3, 13, 23, 30);
        vinteETresEMeiaEmBelem.UtcDateTime.Day.Should().Be(14);
        TipoVigenteEm(DiaDaPublicacaoEmBelem);

        Result<AtoMontado> montado = await Montar(Comando(new DateOnly(2026, 3, 10)), vinteETresEMeiaEmBelem);

        montado.IsSuccess.Should().BeTrue();
        await _tipos.Received(1).ObterVigenteAsync(Tipo, DiaDaPublicacaoEmBelem, Arg.Any<CancellationToken>());
    }

    [Theory(DisplayName = "A data que o documento declara, antes ou depois da publicação, não muda o tipo resolvido")]
    [InlineData(2025, 12, 1)]
    [InlineData(2026, 3, 12)]
    [InlineData(2026, 3, 14)]
    [InlineData(2027, 1, 20)]
    public async Task Montar_DataDoAtoDeclarada_NaoEscolheOTipo(int ano, int mes, int dia)
    {
        TipoVigenteEm(DiaDaPublicacaoEmBelem);

        Result<AtoMontado> montado = await Montar(Comando(new DateOnly(ano, mes, dia)), InstanteEmBelem.Em(2026, 3, 13, 10));

        montado.IsSuccess.Should().BeTrue();
        montado.Value!.Ato.DataDoAto.Should().Be(new DateOnly(ano, mes, dia));
        await _tipos.DidNotReceive().ObterVigenteAsync(Tipo, new DateOnly(ano, mes, dia), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Recusa citando a data da publicação quando o tipo não tem versão vigente nela")]
    public async Task Montar_SemTipoVigenteNoDiaDaPublicacao_CitaADataDaPublicacao()
    {
        Result<AtoMontado> montado = await Montar(Comando(DiaDaPublicacaoEmBelem), InstanteEmBelem.Em(2026, 3, 13, 10));

        montado.Error!.Code.Should().Be(AtoNormativoErrorCodes.TipoSemVersaoVigente);
        montado.Error.Message.Should().Contain("data da publicação (2026-03-13)");
    }

    [Fact(DisplayName = "Devolve a recusa do fuso quando a base de fusos não o reconhece, sem consultar o catálogo")]
    public async Task Montar_FusoNaoReconhecido_RecusaSemConsultarOCatalogo()
    {
        MontagemDoAtoNormativo montagem = new(
            _tipos, new ResolvedorFusoIndisponivelDeTeste(), new CadeiaDeRetificacaoDoAto(_atos), new ReservaDasVagasDoObjeto(_atos));

        Result<AtoMontado> montado = await montagem.MontarAsync(
            Comando(DiaDaPublicacaoEmBelem), InstanteEmBelem.Em(2026, 3, 13, 10), CancellationToken.None);

        montado.Error!.Code.Should().Be("ProcessoSeletivo.FusoInstitucionalNaoReconhecido");
        await _tipos.DidNotReceiveWithAnyArgs().ObterVigenteAsync(default!, default, default);
    }

    private Task<Result<AtoMontado>> Montar(RegistrarAtoNormativoCommand comando, DateTimeOffset publicadoEm) =>
        new MontagemDoAtoNormativo(
            _tipos, new ResolvedorFusoDeTeste(), new CadeiaDeRetificacaoDoAto(_atos), new ReservaDasVagasDoObjeto(_atos))
            .MontarAsync(comando, publicadoEm, CancellationToken.None);

    private void TipoVigenteEm(DateOnly dia)
    {
        TipoAtoPublicado tipo = TipoAtoPublicado.Criar(
            Tipo, "Edital de abertura", congelaConfiguracao: false, unicoPorObjeto: false,
            efeitoIrreversivel: false, ehResultado: false, new DateOnly(2026, 1, 1), null, null).Value!;
        _tipos.ObterVigenteAsync(Tipo, dia, Arg.Any<CancellationToken>()).Returns(tipo);
    }

    private static RegistrarAtoNormativoCommand Comando(DateOnly dataDoAto) =>
        new(
            Orgao: "CEPS",
            Serie: "EDITAL",
            Ano: 2026,
            Numero: "13",
            TipoCodigo: Tipo,
            DataDoAto: dataDoAto,
            DocumentoHash: HashValido,
            Assinante: "Jairo Belchior");
}
