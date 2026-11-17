namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A resposta fora das opções é conferida sobre todos os campos que resolveram, junto com a recusa dos que não resolveram:
/// as recusas de resolução vêm primeiro, e depois uma causa por campo de origem incompatível, na ordem do formulário.
/// </summary>
public sealed class RecusasDasOpcoesDasRespostasNoCongelamentoTests
{
    private const string SemValoresOfertados = "ProcessoSeletivo.FatoColetadoSemValoresOfertados";

    [Fact(DisplayName = "Um campo sem valores vigentes e um campo cujas opções não cabem no outro recusam juntos, a resolução primeiro")]
    public void CampoSemValoresECampoForaDasOpcoes_RecusamJuntos()
    {
        ProcessoSeletivo processo = ProcessoComCampos(
            [CampoDoCatalogo("TIPO_DEFICIENCIA", 0), CampoDoProcesso("OPCAO_B", 1, []), CampoDoProcesso("OPCAO_C", 2, ["OPCAO_B"])]);
        Opcoes(processo, "OPCAO_B", "MEDICINA", "DIREITO");
        Opcoes(processo, "OPCAO_C", "MEDICINA");

        Result<IReadOnlyDictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?>> resultado =
            ResolvedorValoresSelecionaveisCongelados.Resolver(processo, Catalogo("TIPO_DEFICIENCIA", "OPCAO_B", "OPCAO_C"));

        resultado.Errors.Select(static e => e.Error.Code).Should().Equal(SemValoresOfertados, ItemFormularioErrorCodes.OpcoesDeOutroDominio);
        resultado.Errors[0].Error.Message.Should().Contain("'TIPO_DEFICIENCIA'");
        resultado.Errors[1].Error.Message.Should().Contain("'OPCAO_B'").And.Contain("'OPCAO_C'");
    }

    private static ProcessoSeletivo ProcessoComDoisParesIncompativeis()
    {
        ProcessoSeletivo processo = ProcessoComCampos(
        [
            CampoDoProcesso("OPCAO_B", 0, []), CampoDoProcesso("OPCAO_C", 1, ["OPCAO_B"]),
            CampoDoProcesso("OPCAO_D", 2, []), CampoDoProcesso("OPCAO_E", 3, ["OPCAO_D"]),
        ]);
        Opcoes(processo, "OPCAO_B", "MEDICINA", "DIREITO");
        Opcoes(processo, "OPCAO_C", "MEDICINA");
        Opcoes(processo, "OPCAO_D", "FISICA", "QUIMICA");
        Opcoes(processo, "OPCAO_E", "FISICA");

        return processo;
    }

    [Fact(DisplayName = "Dois pares de campos incompatíveis recusam juntos, na ordem dos campos")]
    public void DoisParesForaDasOpcoes_RecusamJuntosNaOrdemDosCampos()
    {
        ProcessoSeletivo processo = ProcessoComDoisParesIncompativeis();

        Result<IReadOnlyDictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?>> resultado =
            ResolvedorValoresSelecionaveisCongelados.Resolver(processo, Catalogo("OPCAO_B", "OPCAO_C", "OPCAO_D", "OPCAO_E"));

        resultado.Errors.Select(static e => e.Error.Code).Should().Equal(ItemFormularioErrorCodes.OpcoesDeOutroDominio, ItemFormularioErrorCodes.OpcoesDeOutroDominio);
        resultado.Errors[0].Error.Message.Should().Contain("'OPCAO_B'").And.Contain("'OPCAO_C'");
        resultado.Errors[1].Error.Message.Should().Contain("'OPCAO_D'").And.Contain("'OPCAO_E'");
    }

    [Fact(DisplayName = "Um campo que recebe as opções de duas fontes recusa por cada fonte incompatível, na ordem das fontes")]
    public void UmAlvoComDuasFontesIncompativeis_RecusaPorCadaFonte()
    {
        ProcessoSeletivo processo = ProcessoComCampos(
            [CampoDoProcesso("OPCAO_B", 0, []), CampoDoProcesso("OPCAO_D", 1, []), CampoDoProcesso("OPCAO_C", 2, ["OPCAO_B", "OPCAO_D"])]);
        Opcoes(processo, "OPCAO_B", "MEDICINA", "DIREITO");
        Opcoes(processo, "OPCAO_D", "FISICA");
        Opcoes(processo, "OPCAO_C", "MEDICINA");

        Result<IReadOnlyDictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?>> resultado =
            ResolvedorValoresSelecionaveisCongelados.Resolver(processo, Catalogo("OPCAO_B", "OPCAO_D", "OPCAO_C"));

        resultado.Errors.Select(static e => e.Error.Message).Should().SatisfyRespectively(
            primeira => primeira.Should().Contain("'OPCAO_B'"),
            segunda => segunda.Should().Contain("'OPCAO_D'"));
    }

    private static ProcessoSeletivo ProcessoComCampos(IReadOnlyList<FatoColetado> campos)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            "PS Opções das respostas", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        processo.DefinirItens(campos, finalidade: FinalidadeFormulario.Habilitacao).IsSuccess.Should().BeTrue();

        return processo;
    }

    private static void Opcoes(ProcessoSeletivo processo, string fato, params string[] codigos) =>
        processo.DefinirOpcoesDeclaradas(fato, [.. codigos.Select((c, i) => OpcaoDeclaradaFato.Criar(fato, c, c, i).Value!)])
            .IsSuccess.Should().BeTrue();

    private static FatoColetado CampoDoProcesso(string codigo, int ordem, string[] formadoPor) =>
        FatoColetado.Criar(
            codigo, ordem, codigo, TipoRenderizacao.SelecaoUnica, Obrigatoriedade.Sempre, null, origemValores: OrigemValoresColeta.OpcoesDoProcesso,
            restricoes: formadoPor.Length > 0 ? [new OpcoesDasRespostas(formadoPor)] : [], classificacaoProtecao: "PESSOAL",
            hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!;

    private static FatoColetado CampoDoCatalogo(string codigo, int ordem) =>
        FatoColetado.Criar(
            codigo, ordem, codigo, TipoRenderizacao.SelecaoUnica, Obrigatoriedade.Sempre, null, classificacaoProtecao: "PESSOAL",
            hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!;

    private static Dictionary<string, FatoCandidatoView> Catalogo(params string[] codigos) =>
        codigos.ToDictionary(static c => c, static c => c == "TIPO_DEFICIENCIA" ? TipoDeDeficienciaSemTipoVivo() : FatoDoProcesso(c), StringComparer.Ordinal);

    private static FatoCandidatoView FatoDoProcesso(string codigo) => new(
        Id: Guid.CreateVersion7(), Codigo: codigo, Nome: codigo, Descricao: "Descrição do fato.", Dominio: "CATEGORICO", Origem: "DECLARADO",
        Cardinalidade: "MULTIVALORADO", ValoresDominio: null, PontoResolucao: "INSCRICAO", Binding: $"CAMPO_FORMULARIO:{codigo}",
        ValoresDominioDeclarados: null, FonteValores: "PROCESSO", Ativo: true, ClassificacaoProtecao: "PESSOAL",
        HipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, FinalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, ProtecaoAprovada: true);

    private static FatoCandidatoView TipoDeDeficienciaSemTipoVivo() => FatoDoProcesso("TIPO_DEFICIENCIA") with
    {
        ValoresDominioDeclarados = [new FatoValorDominioViewItem("TEA", "Transtorno do espectro autista", 0, Ativo: false)],
        FonteValores = "TIPOS_DEFICIENCIA",
    };
}
