namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O fato de seleção cuja oferta é do processo e ainda está vazia não impede a pré-visualização nem o formulário do
/// rascunho: o seletor sai vazio, e quem recusa a oferta vazia é a conferência da publicação.
/// </summary>
public sealed class OfertaVaziaNoRascunhoTests
{
    private const string Fato = "CONDICAO_ATENDIMENTO";

    [Fact(DisplayName = "A pré-visualização de um processo cuja oferta de atendimento está vazia responde, sem recusar a oferta")]
    public async Task PreVisualizar_OfertaVazia_Responde()
    {
        ProcessoSeletivo processo = ProcessoComOfertaVazia();
        IProcessoSeletivoRepository repository = RepositorioDo(processo);

        Result<PreVisualizacaoDoProcessoDto> resultado = await PreVisualizarProcessoSeletivoQueryHandler.Handle(
            new PreVisualizarProcessoSeletivoQuery(processo.Id, new PreVisualizacaoDoProcessoInput(null, null, null, null)),
            repository, CatalogoComOFato(), CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.Formularios.SelectMany(static f => f.Itens).Should().ContainSingle(i => i.FatoCodigo == Fato);
    }

    [Fact(DisplayName = "O formulário do rascunho cuja oferta de atendimento está vazia sai com o seletor vazio")]
    public async Task FormularioDoRascunho_OfertaVazia_SaiComOSeletorVazio()
    {
        ProcessoSeletivo processo = ProcessoComOfertaVazia();

        Result<FormularioApresentadoDoProcessoDto> resultado = await ObterFormularioApresentadoDoRascunhoQueryHandler.Handle(
            new ObterFormularioApresentadoDoRascunhoQuery(processo.Id, FinalidadeFormulario.Habilitacao),
            RepositorioDo(processo), CatalogoComOFato(), new ResolvedorFusoDeTeste(), CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.Formulario.Coleta.Itens.Should().ContainSingle(c => c.FatoCodigo == Fato)
            .Which.ValoresSelecionaveis.Should().NotBeNull().And.BeEmpty();
    }

    private static ProcessoSeletivo ProcessoComOfertaVazia()
    {
        ProcessoSeletivo processo = ProcessoSeletivoConformeBuilder.Criar("PS Oferta vazia");
        processo.DefinirItens(
            [FatoColetadoDeTeste.Coletando(Fato, 0).ComRotulo("Condição de atendimento").DoTipo(TipoDeCampo.EscolhaMultipla).Construir()], finalidade: FinalidadeFormulario.Habilitacao).IsSuccess.Should().BeTrue();

        return processo;
    }

    private static IProcessoSeletivoRepository RepositorioDo(ProcessoSeletivo processo)
    {
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterComConfiguracaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);

        return repository;
    }

    private static IFatoCandidatoReader CatalogoComOFato()
    {
        FatoCandidatoView fato = new(
            Id: Guid.CreateVersion7(), Codigo: Fato, Nome: Fato, Descricao: "Descrição do fato.", Dominio: CodigoComRotulo<TipoDoDado>.De(TipoDoDado.Opcao), Origem: CodigoComRotulo<OrigemFato>.De(OrigemFato.Declarado),
            Cardinalidade: CodigoComRotulo<QuantidadeDeValores>.De(QuantidadeDeValores.VariosValores), ValoresDominio: null, PontoResolucao: "INSCRICAO", Binding: $"CAMPO_FORMULARIO:{Fato}",
            ValoresDominioDeclarados: null, FonteValores: CodigoComRotulo<FonteValoresFato>.De(FonteValoresFato.OpcoesDoProcesso), Ativo: true, ClassificacaoProtecao: "PESSOAL",
            HipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, FinalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, ProtecaoAprovada: true);
        IFatoCandidatoReader catalogo = Substitute.For<IFatoCandidatoReader>();
        catalogo.ListarAsync(Arg.Any<CancellationToken>()).Returns(CatalogoDoConjuntoBasico.Com((IReadOnlyList<FatoCandidatoView>)[fato]));

        return catalogo;
    }
}
