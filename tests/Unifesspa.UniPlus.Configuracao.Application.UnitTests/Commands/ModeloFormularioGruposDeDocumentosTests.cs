namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands.CenarioDoHandlerDoModelo;

/// <summary>
/// Os grupos de documentos do conjunto padrão do modelo na escrita: o documento de um grupo é conferido
/// contra o catálogo como o avulso, o grupo é gravado como veio, e o PUT substitui o conjunto inteiro.
/// </summary>
public sealed class ModeloFormularioGruposDeDocumentosTests
{
    private readonly CenarioDoHandlerDoModelo _cenario = new();

    private static DocumentoExigidoDoModeloInput LaudoQueExigeAoMenos(string fato) =>
        new("INSCRICAO", "LAUDO_MEDICO", Quando(fato, true), new IndeferimentoDoDocumentoDoModeloInput(false, "RETIRA_O_ACRESCIMO", []));

    private static ConteudoDoModeloInput ComGrupoDeDocumentos(GrupoDeDocumentosDoModeloInput grupo) =>
        Conteudo(Item("CERTIFICADO", 0)) with { ConjuntoDeDocumentos = ConjuntoDeDocumentosDoModeloBuilder.Vazio().ComGrupos(grupo).Construir() };

    private static ModeloFormulario ExistenteComDocumentosEGrupo()
    {
        DocumentoExigidoDoModelo documento = new(
            "INSCRICAO", "LAUDO_MEDICO", null, new IndeferimentoDoDocumentoDoModelo(false, "RETIRA_O_ACRESCIMO", []), RequisitosDoDocumentoDoModelo.AindaNaoEscolhidos);
        ConjuntoDeDocumentosDoModelo conjunto = new([documento], [new GrupoDeDocumentosDoModelo(1, null, [documento])]);
        ModeloFormulario existente = Existente(null);
        return ModeloFormulario.Criar(
            existente.Codigo, existente.Nome, null, existente.Declaracao, null,
            existente.Conteudo with { ConjuntoDeDocumentos = conjunto },
            new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal)).Value!;
    }

    [Fact(DisplayName = "O grupo de documentos cujo documento cita fato do catálogo que o modelo coleta é gravado como veio")]
    public async Task Criar_GrupoComDocumentoQueCitaFatoColetado_GravaOGrupo()
    {
        ModeloFormulario? gravado = null;
        await _cenario.Repository.AdicionarAsync(Arg.Do<ModeloFormulario>(m => gravado = m), Arg.Any<CancellationToken>());
        GrupoDeDocumentosDoModeloInput grupo = new(
            2, new IndeferimentoDoGrupoDeDocumentosDoModeloInput("RETIRA_O_ACRESCIMO", []), [LaudoQueExigeAoMenos("CERTIFICADO")]);

        Result<Guid> resultado = await _cenario.CriarAsync(ComGrupoDeDocumentos(grupo));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        GrupoDeDocumentosDoModelo gravada = gravado!.Conteudo.Documentos.GruposDeDocumentos.Should().ContainSingle().Subject;
        gravada.QuantidadeMinima.Should().Be(2);
        gravada.Indeferimento!.ConsequenciaDoDescumprimento.Should().Be("RETIRA_O_ACRESCIMO");
        gravada.Documentos.Should().ContainSingle().Which.TipoDocumentoCodigo.Should().Be("LAUDO_MEDICO");
    }

    [Fact(DisplayName = "A condição de um documento de grupo que cita fato fora do catálogo é recusada no caminho do documento do grupo")]
    public async Task Criar_DocumentoDeGrupoCitaFatoForaDoCatalogo_RecusaNoCaminhoDoGrupo()
    {
        Result<Guid> resultado = await _cenario.CriarAsync(ComGrupoDeDocumentos(ConjuntoDeDocumentosDoModeloBuilder.Grupo(1, LaudoQueExigeAoMenos("NAO_EXISTE"))));

        resultado.Errors.Should().Contain(static e =>
            e.Field == "conteudo.conjuntoDeDocumentos.gruposDeDocumentos[0].documentos[0].quando" && e.Error.Code == "PredicadoDnf.FatoDesconhecido");
    }

    [Fact(DisplayName = "O PUT que omite o conjunto de documentos apaga os documentos avulsos e os grupos")]
    public async Task Atualizar_SemConjuntoDeDocumentos_ApagaDocumentosEGrupos()
    {
        ModeloFormulario existente = ExistenteComDocumentosEGrupo();
        existente.Conteudo.Documentos.GruposDeDocumentos.Should().ContainSingle();

        Result resultado = await _cenario.AtualizarAsync(existente, Conteudo(Item("CERTIFICADO", 0)), null);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        existente.Conteudo.Documentos.DocumentosExigidos.Should().BeEmpty();
        existente.Conteudo.Documentos.GruposDeDocumentos.Should().BeEmpty();
    }
}
