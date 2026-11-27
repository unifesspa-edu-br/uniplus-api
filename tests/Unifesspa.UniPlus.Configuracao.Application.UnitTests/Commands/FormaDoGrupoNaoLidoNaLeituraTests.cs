namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Application.Commands.ModelosFormulario;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands.CenarioDoHandlerDoModelo;

/// <summary>
/// O grupo que a leitura recusa tem a forma conferida mesmo assim, e as recusas dele saem juntas, na ordem das regras:
/// as do próprio grupo, depois as do candidato como membro, por último as dos campos (ADR-0125).
/// </summary>
public sealed class FormaDoGrupoNaoLidoNaLeituraTests
{
    private static readonly Dictionary<string, string> SemFormatos = new(StringComparer.Ordinal);

    [Fact(DisplayName = "O grupo sem mínimo e com máximo zero devolve as duas recusas, o mínimo antes do máximo")]
    public void Ler_GrupoSemMinimoEMaximoZero_RecusaOMinimoEOMaximo()
    {
        GrupoColetadoInput grupo = Grupo("COMPOSICAO", 0) with { Ocorrencias = new OcorrenciasDoGrupoInput(null, false, 0) };

        IReadOnlyList<(string Campo, string Codigo)> recusas = Ler(grupo);

        recusas.Should().Equal(
            ("grupos[0].ocorrencias.minimo", GrupoFormularioErrorCodes.OcorrenciasObrigatorias),
            ("grupos[0].ocorrencias.maximo", GrupoFormularioErrorCodes.ContagemIncoerente));
    }

    [Fact(DisplayName = "O grupo não lido na segunda posição é recusado com o índice dele na entrada")]
    public void Ler_GrupoNaoLidoDepoisDeUmLido_RecusaComOIndiceDaEntrada()
    {
        GrupoColetadoInput lido = Grupo("PRIMEIRO", 0);
        GrupoColetadoInput naoLido = Grupo("SEGUNDO", 1) with { Ocorrencias = new OcorrenciasDoGrupoInput(null, false, 0) };

        IReadOnlyList<(string Campo, string Codigo)> recusas = Ler(lido, naoLido);

        recusas.Select(static r => r.Campo).Should().Equal("grupos[1].ocorrencias.minimo", "grupos[1].ocorrencias.maximo");
    }

    [Fact(DisplayName = "O candidato incluído sem mínimo e sem parentesco recusa o mínimo e o parentesco, sem pedir uma ocorrência")]
    public void Ler_CandidatoIncluidoSemMinimoESemParentesco_RecusaOMinimoEOParentesco()
    {
        GrupoColetadoInput grupo = Grupo("COMPOSICAO", 0) with { Ocorrencias = new OcorrenciasDoGrupoInput(null, true, 10) };

        IReadOnlyList<(string Campo, string Codigo)> recusas = Ler(grupo);

        recusas.Should().Equal(
            ("grupos[0].ocorrencias.minimo", GrupoFormularioErrorCodes.OcorrenciasObrigatorias),
            ("grupos[0].subitens", GrupoFormularioErrorCodes.CandidatoComoMembroIncompleto));
    }

    [Fact(DisplayName = "O parentesco que a leitura recusa não é acusado como ausente")]
    public void Ler_ParentescoNaoLido_NaoAcusaOParentescoAusente()
    {
        GrupoColetadoInput grupo = EntradaDoGrupoColetadoDeTeste.Compondo("COMPOSICAO").ComOcorrencias(1, 10).IncluindoOCandidato()
            .ComObrigatoriedade("SEMPRE").ComCampos(Montar(CandidatoComoMembro.FatoParentesco, 0).NaSecao(null).ComObrigatoriedade("INEXISTENTE").Construir()).Construir();

        IReadOnlyList<(string Campo, string Codigo)> recusas = Ler(grupo);

        recusas.Should().Equal(("grupos[0].subitens[0].regras.obrigatoriedade", ModeloFormularioErrorCodes.ObrigatoriedadeInvalida));
    }

    [Fact(DisplayName = "A obrigatoriedade inválida e a exibição que cita o próprio grupo saem juntas")]
    public void Ler_ObrigatoriedadeInvalidaEExibicaoAutorreferente_RecusaAsDuas()
    {
        GrupoColetadoInput grupo = EntradaDoGrupoColetadoDeTeste.Compondo("COMPOSICAO").ComOcorrencias(0, 5)
            .ExibidoQuando(Quando("COMPOSICAO", true)).ComObrigatoriedade("TALVEZ").ComCampos(Campo("MAIOR_IDADE", 0)).Construir();

        IReadOnlyList<(string Campo, string Codigo)> recusas = Ler(grupo);

        recusas.Select(static r => r.Codigo).Should().Equal(
            ModeloFormularioErrorCodes.ObrigatoriedadeInvalida, GrupoFormularioErrorCodes.RegraAutorreferente);
    }

    [Fact(DisplayName = "As recusas do grupo saem antes das dos campos, e o campo lido tem a forma conferida")]
    public void Ler_GrupoNaoLidoComCampoLidoSemRotulo_RecusaOGrupoAntesDoCampo()
    {
        GrupoColetadoInput grupo = EntradaDoGrupoColetadoDeTeste.Compondo("COMPOSICAO").ComRotulo(string.Empty).ComOcorrencias(0, 5)
            .ComObrigatoriedade("TALVEZ").ComCampos(Montar("MAIOR_IDADE", 0).NaSecao(null).ComRotulo(string.Empty).Construir()).Construir();

        IReadOnlyList<(string Campo, string Codigo)> recusas = Ler(grupo);

        recusas.Select(static r => r.Campo).Should().Equal("grupos[0].regras.obrigatoriedade", "grupos[0].rotulo", "grupos[0].subitens[0].apresentacao.rotulo");
    }

    [Fact(DisplayName = "As recusas dos campos saem na ordem da entrada, o campo lido antes do que não se leu")]
    public void Ler_CampoLidoSemRotuloAntesDeCampoNaoLido_RecusaNaOrdemDaEntrada()
    {
        GrupoColetadoInput grupo = EntradaDoGrupoColetadoDeTeste.Compondo("COMPOSICAO").ComRotulo("Composição").ComOcorrencias(0, 5).ComObrigatoriedade("SEMPRE")
            .ComCampos(
                Montar("MAIOR_IDADE", 0).NaSecao(null).ComRotulo(string.Empty).Construir(),
                Montar("SEM_RENDA", 1).NaSecao(null).DoTipo("texto").Construir()).Construir();

        IReadOnlyList<(string Campo, string Codigo)> recusas = Ler(grupo);

        recusas.Select(static r => r.Campo).Should().Equal("grupos[0].subitens[0].apresentacao.rotulo", "grupos[0].subitens[1].apresentacao.tipoDeCampo");
    }

    private static IReadOnlyList<(string Campo, string Codigo)> Ler(params GrupoColetadoInput[] grupos)
    {
        LeituraDoConteudo leitura = new(SemFormatos);
        leitura.Ler(new ConteudoDoModeloInput("Habilitação", null, null, null, null, grupos));
        return [.. leitura.Recusas.Select(static e => (e.Field!, e.Error.Code))];
    }

    private static GrupoColetadoInput Grupo(string codigo, int ordem) =>
        EntradaDoGrupoColetadoDeTeste.Compondo(codigo, ordem).ComRotulo("Composição").ComOcorrencias(0, 5).ComObrigatoriedade("SEMPRE")
            .ComCampos(Campo("MAIOR_IDADE", 0)).Construir();
}
