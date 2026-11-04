namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using System.Text.Json;

using Domain.Entities;
using Domain.ProcessosSeletivos.Edicao;
using Domain.ProcessosSeletivos.Exigencias;
using Domain.Services;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// Resolve as condições de gatilho de uma folha. Monta uma <see cref="CondicaoDnf"/> por linha, agrupa em
/// <see cref="PredicadoDnf"/> para validar fato no vocabulário fechado, operador contra domínio e valor
/// contra domínio — estático ou dinâmico, contra a oferta do processo — e só então converte para as
/// entidades <see cref="CondicaoGatilho"/> que o domínio persiste.
/// </summary>
internal static class CondicoesDaExigencia
{
    /// <summary>
    /// Resolve e confere as condições da folha, inclusive o gate de fase: a condição não pode citar fato
    /// cuja fase de resolução é posterior à fase em que o documento é exigido, porque o gatilho não teria
    /// como já estar resolvido para o candidato (UNI-REQ-0077, UNI-REQ-0144).
    /// </summary>
    public static Result<IReadOnlyList<CondicaoGatilho>> Resolver(
        ItemDocumentoExigidoInput folha, ProcessoSeletivo processo, FatosDoGatilho fatos, string? grupoDaRepeticao)
    {
        Result<IReadOnlyList<CondicaoGatilho>> condicoes = Montar(folha.Condicoes, fatos, UniversoDaFolha(processo, fatos, grupoDaRepeticao));
        if (condicoes.IsFailure || condicoes.Value!.Count == 0)
        {
            return condicoes;
        }

        Result gate = ConferirGateDeFase(condicoes.Value, folha, processo, fatos);
        return gate.IsFailure ? Result<IReadOnlyList<CondicaoGatilho>>.Failure(gate.Error!) : condicoes;
    }

    // A folha dentro de uma subárvore repetida cita, além dos fatos do candidato, os campos do grupo pelo
    // qual se repete, avaliados em cada ocorrência (ADR-0138). Fora dela, o campo de membro não está no
    // universo e é recusado.
    private static HashSet<string> UniversoDaFolha(ProcessoSeletivo processo, FatosDoGatilho fatos, string? grupoDaRepeticao) =>
        new(fatos.Resolviveis.Concat(RegrasDasExigencias.CamposDoGrupo(EstadoDaConfiguracao.De(processo), grupoDaRepeticao)), StringComparer.Ordinal);

    private static Result<IReadOnlyList<CondicaoGatilho>> Montar(
        IReadOnlyList<CondicaoGatilhoInput> declaradas, FatosDoGatilho fatos, IReadOnlySet<string> universo)
    {
        if (declaradas.Count == 0)
        {
            return Result<IReadOnlyList<CondicaoGatilho>>.Success([]);
        }

        Result<List<(int Clausula, CondicaoDnf Condicao)>> linhas = Linhas(declaradas);
        Result validacao = linhas.IsFailure ? Result.Failure(linhas.Error!) : Validar(linhas.Value!, fatos, universo);
        if (validacao.IsFailure)
        {
            return Result<IReadOnlyList<CondicaoGatilho>>.Failure(validacao.Error!);
        }

        return Gatilhos(linhas.Value!);
    }

    private static Result<List<(int Clausula, CondicaoDnf Condicao)>> Linhas(IReadOnlyList<CondicaoGatilhoInput> declaradas)
    {
        List<(int Clausula, CondicaoDnf Condicao)> linhas = [];
        foreach (CondicaoGatilhoInput declarada in declaradas)
        {
            Result<CondicaoDnf> condicao = CondicaoDnf.Criar(
                declarada.Fato, OperadorCodigo.FromCodigo(declarada.Operador), InterpretarValor(declarada.Valor));
            if (condicao.IsFailure)
            {
                return Result<List<(int Clausula, CondicaoDnf Condicao)>>.Failure(condicao.Error!);
            }

            linhas.Add((declarada.Clausula, condicao.Value!));
        }

        return Result<List<(int Clausula, CondicaoDnf Condicao)>>.Success(linhas);
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

    // A fase efetiva do fato é conferida por ProcessoSeletivo.RecusaDeFaseDoGatilho, e o fato só da isenção
    // citado por documento de outro formulário também. A fase da própria exigência fora do cronograma, e a
    // finalidade que falta ou que a fase não responde, são decididas por DefinirDocumentosExigidos.
    private static Result ConferirGateDeFase(
        IReadOnlyList<CondicaoGatilho> condicoes, ItemDocumentoExigidoInput folha, ProcessoSeletivo processo, FatosDoGatilho fatos)
    {
        EstadoDaConfiguracao estado = EstadoDaConfiguracao.De(processo);
        FinalidadeFormulario? finalidade = LeituraDaExigenciaDoPayload.FinalidadeDoToken(folha.Finalidade);
        DomainError? recusa = condicoes
            .Select(c => FaseEfetivaDoFato.RecusaDeFaseDoGatilho(
                estado, c.Fato, folha.ExigidoNaFaseId, finalidade, fatos.PontoResolucaoPorFato, fatos.MembroPorAgregado))
            .FirstOrDefault(static r => r is not null);
        return recusa is null ? Result.Success() : Result.Failure(recusa);
    }

    // Texto JSON válido (booleano, número, string entre aspas, array para EM) é interpretado como tal; o
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
