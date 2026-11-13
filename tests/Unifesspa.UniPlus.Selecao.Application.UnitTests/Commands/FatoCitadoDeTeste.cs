namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O fato <see cref="Fato"/> que o formulário coleta e a exigência de documento cita no gatilho, como a
/// versão publicada o congelou e como o catálogo vivo o descreve hoje.
/// </summary>
internal static class FatoCitadoDeTeste
{
    public const string Fato = "TEM_RENDA";
    public const string FatoNovo = "TEM_ENDERECO_PROPRIO";
    public const string CodigoDaRecusaDoEixoAlterado = "RascunhoRetificacao.EixoDoFatoJaColetadoAlterado";

    public static FatoColetado Item(TipoRenderizacao tipo) =>
        ItemColetado(Fato, 0, "Tem renda?", tipo, Obrigatoriedade.Sempre);

    // Um fato que nenhuma versão publicada coletou, com a cópia velha do catálogo (PESSOAL).
    public static FatoColetado ItemNovo() =>
        ItemColetado(FatoNovo, 100, "Tem endereço próprio?", TipoRenderizacao.Booleano, Obrigatoriedade.Nunca);

    public static DocumentoExigido ExigenciaComGatilho(Guid faseId) =>
        DocumentoExigido.Criar(
            faseId, CadastrosVivos.IdentidadeDe("LAUDO_MEDICO"), "DECLARACAO", "Declaração", "PESSOAL", ProtecaoDoDocumentoDeTeste.Pessoal,
            Aplicabilidade.Condicional, obrigatorio: true, consequenciaIndeferimento: null,
            condicoes: [CondicaoGatilho.Criar(0, Fato, Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!],
            basesLegais: [DocumentoExigidoBaseLegal.Criar("Res. Unifesspa 532/2021, art. 12", TipoAbrangencia.InternaNorma, StatusBaseLegal.Resolvido, null).Value!],
            idadeMaximaEmissao: null, formatosPermitidos: FormatosPermitidos.Criar(true, null).Value!,
            tamanhoMaximoBytes: null, finalidade: FinalidadeFormulario.Inscricao).Value!;

    /// <summary>O processo conforme que coleta o fato e exige um documento sob o gatilho dele.</summary>
    public static ProcessoSeletivo ProcessoQueColetaOFato(bool comItemNovo)
    {
        ProcessoSeletivo processo = ProcessoSeletivoConformeBuilder.Criar("PS Eixos nos handlers", out Guid faseId);
        processo.DefinirItens(comItemNovo ? [Item(TipoRenderizacao.Booleano), ItemNovo()] : [Item(TipoRenderizacao.Booleano)], PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();
        processo.DefinirDocumentosExigidos([NoExigencia.CriarFolha(ExigenciaComGatilho(faseId), 0).Value!], PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();

        return processo;
    }

    // O que a versão publicada congelou do item vem de um processo que só coleta o fato com esse tipo.
    public static ProcessoSeletivo ProcessoSoComOItemPublicado(TipoRenderizacao tipo)
    {
        ProcessoSeletivo outro = ProcessoSeletivoConformeBuilder.Criar("PS Item publicado");
        outro.DefinirItens([Item(tipo)], PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();

        return outro;
    }

    public static EixosDeVersaoPublicada EixosCongelados(string dominio) => new(
        new Dictionary<string, EixosDoMetadadoCongelado> { [Fato] = new(dominio, "ESCALAR", "RESULTADO_FINAL") },
        new Dictionary<string, string>());

    public static MetadadoFatoCongelado MetadadoCongelado(EixosDeVersaoPublicada congelado) =>
        new(Fato, congelado.Metadados[Fato].Dominio, "DECLARADO", "ESCALAR", "RESULTADO_FINAL", $"CAMPO_FORMULARIO:{Fato}", null, null);

    public static FatoCandidatoView FatoDoCatalogo(string dominio, string classificacao) => new(
        Guid.CreateVersion7(), Fato, "Tem renda?", "Descrição do fato.", dominio, "DECLARADO", "ESCALAR", null,
        "RESULTADO_FINAL", $"CAMPO_FORMULARIO:{Fato}", null, null, Ativo: true, ClassificacaoProtecao: classificacao,
        HipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, FinalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, ProtecaoAprovada: true);

    public static FatoCandidatoView FatoNovoDoCatalogo(FatoCandidatoView fato) =>
        fato with { Id = Guid.CreateVersion7(), Codigo = FatoNovo, Nome = "Tem endereço próprio?", Dominio = "BOOLEANO", Binding = $"CAMPO_FORMULARIO:{FatoNovo}" };

    private static FatoColetado ItemColetado(string fato, int ordem, string rotulo, TipoRenderizacao tipo, Obrigatoriedade obrigatoriedade) =>
        FatoColetado.Criar(
            fato, ordem, rotulo, tipo, obrigatoriedade, null, classificacaoProtecao: "PESSOAL", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal,
            finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!;
}
