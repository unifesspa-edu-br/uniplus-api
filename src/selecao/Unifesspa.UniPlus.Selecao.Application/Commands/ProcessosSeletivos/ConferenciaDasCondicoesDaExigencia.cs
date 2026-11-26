namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using System.Text.Json;

using Domain.Entities;
using Domain.ProcessosSeletivos.Exigencias;
using Domain.Services;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// Resolve as condições de gatilho de uma regra de documento. Monta uma <see cref="CondicaoDnf"/> por linha, agrupa em
/// <see cref="PredicadoDnf"/> para validar fato no vocabulário fechado, operador contra domínio e valor
/// contra domínio — estático ou dinâmico, contra o atendimento especializado do processo — e só então converte para as
/// entidades <see cref="CondicaoGatilho"/> que o domínio persiste.
/// </summary>
internal static class ConferenciaDasCondicoesDaExigencia
{
    /// <summary>
    /// Resolve e confere as condições da regra de documento, inclusive a fase dos fatos: a condição não pode citar fato
    /// cuja fase de resolução é posterior à fase em que o documento é exigido, porque o gatilho não teria
    /// como já estar resolvido para o candidato (UNI-REQ-0077, UNI-REQ-0144).
    /// </summary>
    public static Result<IReadOnlyList<CondicaoGatilho>> Resolver(
        ItemDocumentoExigidoInput documento, ProcessoSeletivo processo, FatosDoGatilho fatos, string? grupoDaRepeticao)
    {
        Result<IReadOnlyList<CondicaoGatilho>> condicoes = Montar(documento.Condicoes(), fatos, UniversoDaRegraDeDocumento(processo, fatos, grupoDaRepeticao));
        if (condicoes.IsFailure || condicoes.Value!.Count == 0)
        {
            return condicoes;
        }

        Result conferenciaDaFase = ConferirFaseDoGatilho(condicoes.Value, documento, processo, fatos);
        return conferenciaDaFase.IsFailure ? Result<IReadOnlyList<CondicaoGatilho>>.Failure(conferenciaDaFase.Error!) : condicoes;
    }

    // A regra de documento repetida cita, além dos fatos do candidato, os campos do grupo pelo
    // qual se repete, avaliados em cada ocorrência (ADR-0138). Fora dela, o campo de membro não está no
    // universo e é recusado.
    private static HashSet<string> UniversoDaRegraDeDocumento(ProcessoSeletivo processo, FatosDoGatilho fatos, string? grupoDaRepeticao) =>
        new(fatos.Resolviveis.Concat(RegrasDasExigencias.CamposDoGrupo(processo.Declaracao(), grupoDaRepeticao)), StringComparer.Ordinal);

    private static Result<IReadOnlyList<CondicaoGatilho>> Montar(
        IReadOnlyList<CondicaoGatilhoInput> declaradas, FatosDoGatilho fatos, IReadOnlySet<string> universo)
    {
        if (declaradas.Count == 0)
        {
            return Result<IReadOnlyList<CondicaoGatilho>>.Success([]);
        }

        Result<List<(int Clausula, CondicaoDnf Condicao)>> linhas = Linhas(declaradas);
        return linhas.IsFailure
            ? Result<IReadOnlyList<CondicaoGatilho>>.ValidationFailure(linhas.Errors)
            : Conferidas(linhas.Value!, fatos, universo);
    }

    private static Result<IReadOnlyList<CondicaoGatilho>> Conferidas(
        List<(int Clausula, CondicaoDnf Condicao)> linhas, FatosDoGatilho fatos, IReadOnlySet<string> universo)
    {
        Result validacao = Validar(linhas, fatos, universo);
        return validacao.IsFailure
            ? Result<IReadOnlyList<CondicaoGatilho>>.Failure(validacao.Error!)
            : Gatilhos(linhas);
    }

    private static Result<List<(int Clausula, CondicaoDnf Condicao)>> Linhas(IReadOnlyList<CondicaoGatilhoInput> declaradas)
    {
        List<(int Clausula, CondicaoDnf Condicao)> linhas = [];
        List<FieldError> recusas = [];
        for (int i = 0; i < declaradas.Count; i++)
        {
            LerCondicao(declaradas[i], i, linhas, recusas);
        }

        return recusas.Count > 0
            ? Result<List<(int Clausula, CondicaoDnf Condicao)>>.ValidationFailure(recusas)
            : Result<List<(int Clausula, CondicaoDnf Condicao)>>.Success(linhas);
    }

    private static void LerCondicao(
        CondicaoGatilhoInput declarada, int posicao, List<(int Clausula, CondicaoDnf Condicao)> linhas, List<FieldError> recusas)
    {
        Result<CondicaoDnf> condicao = CondicaoDnf.CriarInformada(declarada.Fato, declarada.Operador, InterpretarValor(declarada.Valor));
        recusas.AddRange(condicao.Errors.Select(recusa => recusa.NoCaminhoDe($"{AplicabilidadeDaExigenciaInput.CaminhoDasCondicoes}[{posicao}]")));
        if (condicao.IsSuccess)
        {
            linhas.Add((declarada.Clausula, condicao.Value!));
        }
    }

    // O universo de fatos que o processo resolve entra aqui, e não nulo: um gatilho que cita fato que o
    // certame não coleta, não deriva e não obtém do candidato nunca se resolve para ninguém, e a exigência
    // ficaria pendente para sempre — pior que recusar, porque parece funcionar.
    private static Result Validar(
        List<(int Clausula, CondicaoDnf Condicao)> linhas, FatosDoGatilho fatos, IReadOnlySet<string> universo)
    {
        Result<PredicadoDnf> predicado = PredicadoDnf.CriarDeCondicoesAgrupadas(linhas);
        return predicado.IsFailure
            ? Result.Failure(predicado.Error!)
            : PredicadoDnfValidador.Validar(predicado.Value!, fatos.Vocabulario, universo, fatos.DominiosDinamicos);
    }

    private static Result<IReadOnlyList<CondicaoGatilho>> Gatilhos(List<(int Clausula, CondicaoDnf Condicao)> linhas)
    {
        List<CondicaoGatilho> condicoes = [];
        foreach ((int clausula, CondicaoDnf condicao) in linhas)
        {
            Result<CondicaoGatilho> gatilho = CondicaoGatilho.Criar(clausula, condicao.Fato, condicao.Operador, condicao.Valor);
            if (gatilho.IsFailure)
            {
                return Result<IReadOnlyList<CondicaoGatilho>>.Failure(gatilho.Error!);
            }

            condicoes.Add(gatilho.Value!);
        }

        return Result<IReadOnlyList<CondicaoGatilho>>.Success(condicoes);
    }

    // A fase efetiva do fato, e o formulário que a exigência cita, são conferidos por
    // ProcessoSeletivo.RecusaDeFaseDoGatilho. A fase da própria exigência fora do cronograma, e o código do
    // formulário que falta ou que a fase não responde, são decididos por DefinirDocumentosExigidos.
    private static Result ConferirFaseDoGatilho(
        IReadOnlyList<CondicaoGatilho> condicoes, ItemDocumentoExigidoInput documento, ProcessoSeletivo processo, FatosDoGatilho fatos)
    {
        ConfiguracaoDeclarada configuracao = processo.Declaracao();
        FinalidadeFormulario? finalidade = EscritaDaExigencia.FinalidadeDoCodigo(documento.FormularioCodigo());
        DomainError? recusa = condicoes
            .Select(c => FaseEfetivaDoFato.RecusaDeFaseDoGatilho(
                configuracao, c.Fato, documento.FaseId(), finalidade, fatos.PontoResolucaoPorFato, fatos.MembroPorAgregado))
            .FirstOrDefault(static r => r is not null);
        return recusa is null ? Result.Success() : Result.Failure(recusa);
    }

    // Texto JSON válido (booleano, número, string entre aspas, array para ALGUM_DE) é interpretado como tal; o
    // que não é JSON válido é o escalar de string que representa.
    private static JsonElement InterpretarValor(string valor)
    {
        try
        {
            using JsonDocument documento = JsonDocument.Parse(valor);
            return documento.RootElement.Clone();
        }
        catch (JsonException)
        {
            return JsonSerializer.SerializeToElement(valor);
        }
    }
}
