namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Queries.Providencias;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>A leitura das providências: a situação derivada no instante e o registro de cada providência exibida (ADR-0153).</summary>
public sealed class LeituraDaProvidenciaTests
{
    private const string Administrar = "autorizacao:concessoes:administrar";
    private const string Solicitar = "autorizacao:providencias:solicitar";

    private readonly CenarioDaProvidencia _cenario = new();
    private readonly ProvidenciaComConcessao _item;

    public LeituraDaProvidenciaTests()
    {
        ProvidenciaBuilder construtor = new();
        _item = new(construtor.Aberta(), new ConcessaoDaProvidencia(construtor.Concessao, CenarioDaProvidencia.PerfilNome));
        _cenario.Providencias.ObterComConcessaoAsync(_item.Id, Arg.Any<CancellationToken>()).Returns(_item);
        _cenario.Providencias.ListarPaginadoAsync(default!, default, default, default, default, default).ReturnsForAnyArgs(([_item], null, null));
    }

    [Fact(DisplayName = "Quem administra concessões lê o detalhe, e a leitura grava a permissão de administrar para a providência")]
    public async Task Obter_PorQuemAdministra_RegistraAdministrar()
    {
        VerificadorDeTeste verificador = new("diogo.souza", [Administrar, Solicitar]);

        Result<ProvidenciaDto> resultado = await ObterAsync(verificador, _item.Id);

        resultado.Value!.Fundamento.Should().Be(ProvidenciaBuilder.Fundamento);
        RegistroDecisaoAcesso registro = verificador.Gravados.Should().ContainSingle().Subject.Should().ContainSingle().Subject;
        (registro.Permissao, registro.ResourceTipo, registro.ResourceId).Should().Be((Administrar, "providencia-sobre-concessao", (Guid?)_item.Id));
    }

    [Fact(DisplayName = "O Encarregado lê pela permissão de solicitar, que nomeia o registro")]
    public async Task Obter_PeloEncarregado_RegistraSolicitar()
    {
        VerificadorDeTeste verificador = new("cecilia.moura", [Solicitar]);

        (await ObterAsync(verificador, _item.Id)).Value!.ImpedidoDeResponder.Should().BeTrue();

        verificador.Gravados.Single().Single().Permissao.Should().Be(Solicitar);
    }

    [Fact(DisplayName = "A providência inexistente responde não encontrada, sem registro")]
    public async Task Obter_Inexistente_NaoEncontrada()
    {
        VerificadorDeTeste verificador = new("diogo.souza", [Administrar]);

        (await ObterAsync(verificador, Guid.CreateVersion7())).Error!.Code.Should().Be(ProvidenciaErrorCodes.NaoEncontrada);
        verificador.Gravados.Should().BeEmpty();
    }

    [Fact(DisplayName = "A fila pede a situação no instante da leitura, repassa a âncora do cursor e registra cada providência exibida")]
    public async Task Listar_FiltraERegistraCadaUma()
    {
        VerificadorDeTeste verificador = new("diogo.souza", [Administrar]);
        ListarProvidenciasQuery query = new(SituacaoDaProvidencia.Aberta, null, "chave", null, 20, PaginationDirection.Next);

        ListarProvidenciasResult resultado = await ProvidenciaQueryHandler.Handle(query, Leitura(), verificador, Usuarios.De(ProvidenciaBuilder.Diogo), CancellationToken.None);

        resultado.Items.Should().ContainSingle().Which.Situacao.Should().Be("aberta");
        verificador.Gravados.Single().Select(static r => r.ResourceId).Should().Equal(_item.Id);
        await _cenario.Providencias.Received(1).ListarPaginadoAsync(
            Arg.Is<FiltroDeProvidencias>(f => f.Situacao == SituacaoDaProvidencia.Aberta && f.Agora == ProvidenciaBuilder.Agora), "chave", null, 20, PaginationDirection.Next, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A fila vazia não registra nada")]
    public async Task Listar_Vazia_NaoRegistra()
    {
        VerificadorDeTeste verificador = new("diogo.souza", [Administrar]);
        _cenario.Providencias.ListarPaginadoAsync(default!, default, default, default, default, default).ReturnsForAnyArgs(([], null, null));

        await ProvidenciaQueryHandler.Handle(new ListarProvidenciasQuery(null, null, null, null, 20, PaginationDirection.Next), Leitura(), verificador, Usuarios.De(ProvidenciaBuilder.Diogo), CancellationToken.None);

        verificador.Gravados.Should().BeEmpty();
    }

    private Task<Result<ProvidenciaDto>> ObterAsync(VerificadorDeTeste verificador, Guid id) =>
        ProvidenciaQueryHandler.Handle(new ObterProvidenciaQuery(id), Leitura(), verificador, Usuarios.De(ProvidenciaBuilder.Cecilia), CancellationToken.None);

    private LeituraDaProvidencia Leitura() => new(_cenario.Providencias, new CenarioDaGovernancaDaVersao.RelogioFixo(ProvidenciaBuilder.Agora));
}
