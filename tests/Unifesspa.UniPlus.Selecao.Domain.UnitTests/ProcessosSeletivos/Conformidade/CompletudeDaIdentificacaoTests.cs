namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Conformidade;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConferenciaDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.LeituraDoDocumento.DocumentoLidoDeTeste;

/// <summary>O tipo, a unidade administradora e a origem dos candidatos ausentes salvam como rascunho e recusam a publicação, uma causa por campo; o que a leitura recusou não é ausência.</summary>
public sealed class CompletudeDaIdentificacaoTests
{
    private static readonly string[] CodigosDosItens =
        ["tipo_processo_nao_declarado", "unidade_administradora_nao_declarada", "origem_candidatos_nao_declarada"];

    private static readonly string[] CodigosDasCausas =
    [
        ProcessoSeletivoErrorCodes.TipoProcessoAusente,
        ProcessoSeletivoErrorCodes.UnidadeAdministradoraAusente,
        ProcessoSeletivoErrorCodes.OrigemCandidatosAusente,
    ];

    private static IdentificacaoNoDocumento SemNada() => Identificacao() with
    {
        TipoProcessoCodigo = null,
        UnidadeAdministradoraCodigo = null,
        OrigemCandidatos = null,
    };

    private static (string? Campo, string Codigo)[] Causas(IdentificacaoNoDocumento identificacao) =>
        [.. CausasQueImpedemCongelar.De(Configuracao(Ler(identificacao)), CadastrosDeTeste.Padrao)
            .Where(static causa => CodigosDasCausas.Contains(causa.Error.Code))
            .Select(static causa => (causa.Field, causa.Error.Code))];

    private static ItemConformidade[] Itens(IdentificacaoNoDocumento identificacao) =>
        [.. ChecklistDeConformidade.AvaliarConformidade(Configuracao(Ler(identificacao)), CadastrosDeTeste.Padrao)
            .Where(static item => CodigosDosItens.Contains(item.Codigo))];

    [Fact(DisplayName = "A identificação completa não tem causa de campo ausente")]
    public void Identificacao_Completa_NaoTemCausa()
    {
        Causas(Identificacao()).Should().BeEmpty();
    }

    [Fact(DisplayName = "Tipo, unidade e origem ausentes geram uma causa cada, na ordem do bloco do documento")]
    public void CamposAusentes_GeramUmaCausaCada_NaOrdemDoBloco()
    {
        Causas(SemNada()).Should().Equal(
            ("identificacao.tipoProcessoCodigo", ProcessoSeletivoErrorCodes.TipoProcessoAusente),
            ("identificacao.unidadeAdministradoraCodigo", ProcessoSeletivoErrorCodes.UnidadeAdministradoraAusente),
            ("identificacao.origemCandidatos", ProcessoSeletivoErrorCodes.OrigemCandidatosAusente));
    }

    [Fact(DisplayName = "As causas de identificação vêm antes das de conformidade e do identificador legível")]
    public void CausasDaIdentificacao_VemNoComecoDaConferencia()
    {
        IReadOnlyList<FieldError> causas = CausasQueImpedemCongelar.De(
            Configuracao(Ler(SemNada() with { IdentificadorLegivel = null })), CadastrosDeTeste.Padrao);

        causas.Select(static causa => causa.Error.Code).Take(4).Should().Equal(
            ProcessoSeletivoErrorCodes.TipoProcessoAusente,
            ProcessoSeletivoErrorCodes.UnidadeAdministradoraAusente,
            ProcessoSeletivoErrorCodes.OrigemCandidatosAusente,
            ProcessoSeletivoErrorCodes.IdentificadorLegivelAusente);
    }

    [Fact(DisplayName = "O tipo que a leitura recusou não é ausência: a causa é a da leitura e a completude não a repete")]
    public void TipoRecusadoPelaLeitura_NaoSaiEmDobro()
    {
        Causas(Identificacao() with { TipoProcessoCodigo = "TIPO-DIGITADO" }).Should().BeEmpty();
    }

    [Fact(DisplayName = "Os itens do painel ficam pendentes para o ausente e para o recusado, e aprovados só na identificação completa")]
    public void Itens_RefletemAusenciaERecusa()
    {
        Itens(Identificacao()).Should().OnlyContain(static item => item.Ok).And.HaveCount(3);
        Itens(SemNada()).Should().OnlyContain(static item => !item.Ok);
        Itens(Identificacao() with { TipoProcessoCodigo = "TIPO-DIGITADO" }).Where(static item => !item.Ok).Select(static item => item.Codigo)
            .Should().Equal("tipo_processo_nao_declarado");
    }

    [Fact(DisplayName = "A origem ausente deixa o item da inscrição própria pendente, e não aprovado por omissão")]
    public void OrigemAusente_DeixaAInscricaoPropriaPendente()
    {
        ItemConformidade[] itens =
        [.. ChecklistDeConformidade.AvaliarConformidade(Configuracao(Ler(Identificacao() with { OrigemCandidatos = null })), CadastrosDeTeste.Padrao)
            .Where(static item => item.Codigo == "cronograma_inscricao_propria_sem_fase_de_coleta")];

        itens.Should().ContainSingle().Which.Ok.Should().BeFalse();
    }
}
