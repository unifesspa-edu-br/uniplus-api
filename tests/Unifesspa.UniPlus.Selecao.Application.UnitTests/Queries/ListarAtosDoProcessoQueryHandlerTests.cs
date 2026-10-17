namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using System.Reflection;

using AwesomeAssertions;

using Microsoft.Extensions.Options;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.AtosDoProcesso;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A consulta administrativa junta o ato que criou versão e o registro do que não cria, do mais
/// recente para o mais antigo, e destaca a pendência que passou do limiar.
/// </summary>
public sealed class ListarAtosDoProcessoQueryHandlerTests
{
    private const string HashDoEdital = "9f2c1e0b7a4d5c6e8f0a1b2c3d4e5f60718293a4b5c6d7e8f90a1b2c3d4e5f60";
    private static readonly DateTimeOffset Agora = new(2026, 12, 10, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Limiar = TimeSpan.FromMinutes(30);

    private readonly Guid _processoId = Guid.CreateVersion7();
    private readonly IProcessoSeletivoRepository _processos = Substitute.For<IProcessoSeletivoRepository>();
    private readonly IAtoDoProcessoRepository _atos = Substitute.For<IAtoDoProcessoRepository>();
    private readonly ICertameDivulgadoRepository _divulgados = Substitute.For<ICertameDivulgadoRepository>();
    private readonly IDocumentoAPublicarRepository _documentos = Substitute.For<IDocumentoAPublicarRepository>();

    public ListarAtosDoProcessoQueryHandlerTests()
    {
        _processos.ExisteAsync(_processoId, Arg.Any<CancellationToken>()).Returns(true);
        _processos.ListarResumoDasVersoesAsync(_processoId, Arg.Any<CancellationToken>()).Returns([]);
        _atos.ListarDoProcessoAsync(_processoId, Arg.Any<CancellationToken>()).Returns([]);
        _documentos.ListarPorProcessoAsync(_processoId, null, Arg.Any<CancellationToken>()).Returns([]);
    }

    private Task<Result<ListarAtosDoProcessoResult>> Consultar() => ListarAtosDoProcessoQueryHandler.Handle(
        new ListarAtosDoProcessoQuery(_processoId),
        _processos,
        _atos,
        _divulgados,
        _documentos,
        Options.Create(new AtosDoProcessoOptions { LimiarDePendencia = Limiar }),
        new RelogioFixo(Agora),
        CancellationToken.None);

    private DocumentoAPublicar Confirmado(string hash = HashDoEdital, FinalidadeDoDocumento finalidade = FinalidadeDoDocumento.DocumentoDoEdital)
    {
        DocumentoAPublicar documento = DocumentoAPublicar.IniciarPendente(
            _processoId, finalidade, "documento.pdf", TiposDeMidiaDeDocumento.Pdf, TimeProvider.System, TimeSpan.FromMinutes(15)).Value!;
        documento.Confirmar(2048, hash, TimeProvider.System).IsSuccess.Should().BeTrue();
        return documento;
    }

    private AtoDoProcesso Registro(DocumentoAPublicar principal, DateTimeOffset publicadoEm, bool registrado = false)
    {
        AtoDoProcesso ato = AtoDoProcesso.Registrar(
            new NovoAtoDoProcesso(
                Guid.CreateVersion7(), _processoId, "AVISO", "Aviso", "3/2026", new DateOnly(2026, 12, 10),
                "Diretor do CEPS", "ceps", "psm-medicina-2027", principal.Id, []),
            new RelogioFixo(publicadoEm));
        typeof(AtoDoProcesso).GetProperty(nameof(AtoDoProcesso.CreatedAt))!
            .SetValue(ato, publicadoEm);
        if (registrado)
        {
            ato.ConfirmarRegistro(new RelogioFixo(publicadoEm.AddSeconds(5)));
        }

        return ato;
    }

    [Fact(DisplayName = "Versão do edital e registro entram na mesma lista, do mais recente para o mais antigo")]
    public async Task Handle_VersaoERegistro_OrdenaPorInstanteDecrescente()
    {
        DocumentoAPublicar edital = Confirmado();
        DocumentoAPublicar aviso = Confirmado(new string('b', 64), FinalidadeDoDocumento.DocumentoDeAto);
        ResumoDaVersao versao = new(1, Agora.AddDays(-2), Guid.CreateVersion7(), HashDoEdital, null);
        AtoDoProcesso registro = Registro(aviso, Agora.AddDays(-1), registrado: true);
        _processos.ListarResumoDasVersoesAsync(_processoId, Arg.Any<CancellationToken>()).Returns([versao]);
        _atos.ListarDoProcessoAsync(_processoId, Arg.Any<CancellationToken>()).Returns([registro]);
        _documentos.ListarPorProcessoAsync(_processoId, null, Arg.Any<CancellationToken>()).Returns([aviso, edital]);

        Result<ListarAtosDoProcessoResult> resultado = await Consultar();

        AtoDoProcessoDto[] itens = [.. resultado.Value!.Items];
        itens.Select(i => i.Id).Should().Equal(registro.Id, versao.AtoCriadorId);
        itens[0].CriaVersao.Should().BeFalse();
        itens[0].TipoCodigo.Should().Be("AVISO");
        itens[0].Situacao.Should().Be(SituacaoDoRegistroDoAto.Registrado);
        itens[0].DocumentoPrincipal!.DocumentoAPublicarId.Should().Be(aviso.Id);
        itens[1].CriaVersao.Should().BeTrue();
        itens[1].DocumentoPrincipal!.DocumentoAPublicarId.Should().Be(edital.Id, "o documento do ato que cria versão é o que ele declarou por hash");
    }

    [Fact(DisplayName = "A versão do edital está registrada quando a divulgação registrou o seu edital; sem ela, está pendente")]
    public async Task Handle_Versao_SituacaoVemDaDivulgacao()
    {
        ResumoDaVersao divulgada = new(1, Agora.AddMinutes(-5), Guid.CreateVersion7(), HashDoEdital, null);
        ResumoDaVersao semDivulgacao = new(2, Agora.AddMinutes(-2), Guid.CreateVersion7(), new string('c', 64), divulgada.AtoCriadorId);
        _processos.ListarResumoDasVersoesAsync(_processoId, Arg.Any<CancellationToken>()).Returns([divulgada, semDivulgacao]);
        CertameDivulgado certame = CertameDivulgado.Criar(
            _processoId, 1, divulgada.AtoCriadorId, HashDoEdital, "7",
            new FacetasDoCertameDivulgado("ps-2027", "PS 2027", null, ["AC"], Agora, Agora.AddDays(10)), "{}", Agora);
        certame.RegistrarEdital(new EditalDivulgado(divulgada.AtoCriadorId, 1, "https://acervo.teste/a.pdf"));
        _divulgados.ObterParaLeituraAsync(_processoId, Arg.Any<CancellationToken>()).Returns(certame);

        Result<ListarAtosDoProcessoResult> resultado = await Consultar();

        resultado.Value!.Items.ToDictionary(i => i.Id, i => i.Situacao).Should().Equal(
            new Dictionary<Guid, SituacaoDoRegistroDoAto>
            {
                [semDivulgacao.AtoCriadorId] = SituacaoDoRegistroDoAto.Pendente,
                [divulgada.AtoCriadorId] = SituacaoDoRegistroDoAto.Registrado,
            });
    }

    [Fact(DisplayName = "Só a pendência mais antiga que o limiar vem em destaque")]
    public async Task Handle_Pendencia_DestacaAlemDoLimiar()
    {
        DocumentoAPublicar antigo = Confirmado(new string('d', 64), FinalidadeDoDocumento.DocumentoDeAto);
        DocumentoAPublicar recente = Confirmado(new string('e', 64), FinalidadeDoDocumento.DocumentoDeAto);
        DocumentoAPublicar registradoHaMuito = Confirmado(new string('f', 64), FinalidadeDoDocumento.DocumentoDeAto);
        AtoDoProcesso pendenteAntigo = Registro(antigo, Agora - Limiar - TimeSpan.FromMinutes(1));
        AtoDoProcesso pendenteRecente = Registro(recente, Agora - Limiar + TimeSpan.FromMinutes(1));
        AtoDoProcesso registrado = Registro(registradoHaMuito, Agora.AddDays(-3), registrado: true);
        _atos.ListarDoProcessoAsync(_processoId, Arg.Any<CancellationToken>()).Returns([pendenteRecente, pendenteAntigo, registrado]);
        _documentos.ListarPorProcessoAsync(_processoId, null, Arg.Any<CancellationToken>()).Returns([antigo, recente, registradoHaMuito]);

        Result<ListarAtosDoProcessoResult> resultado = await Consultar();

        resultado.Value!.Items.ToDictionary(i => i.Id, i => i.PendenciaAcimaDoLimiar).Should().Equal(
            new Dictionary<Guid, bool>
            {
                [pendenteRecente.Id] = false,
                [pendenteAntigo.Id] = true,
                [registrado.Id] = false,
            });
    }

    [Fact(DisplayName = "Processo inexistente devolve não encontrado, e não lista vazia")]
    public async Task Handle_ProcessoInexistente_DevolveNaoEncontrado()
    {
        _processos.ExisteAsync(_processoId, Arg.Any<CancellationToken>()).Returns(false);

        Result<ListarAtosDoProcessoResult> resultado = await Consultar();

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
    }
}
