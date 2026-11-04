namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Errors;
using Domain.Services;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// Copia as derivações que o modelo cita, direta ou transitivamente. O que o processo já deriva fica como
/// está; o que não deriva recebe as regras padrão do catálogo, salvo o derivado sem regra padrão, que o
/// processo precisa configurar antes. A modalidade do candidato e a opção pelas cotas da lei vêm da oferta
/// de vagas e nunca são copiadas: quando a cópia as cita, o que elas citam é que recebe as regras padrão.
/// A configuração existente nunca é trocada.
/// </summary>
internal sealed class CopiaDasDerivacoesDoModelo(
    ProcessoSeletivo processo,
    ContextoDoCatalogo contexto,
    IFatoCandidatoReader fatoCandidatoReader,
    RelatorioDaAplicacaoDoModelo relatorio)
{
    private const string FonteModalidade = "MODALIDADE";

    /// <summary>As derivações a copiar para o processo; as recusas entram em <paramref name="erros"/>.</summary>
    public async Task<List<ConfiguracaoDerivacaoInput>> EscolherAsync(
        string[] citados, List<FieldError> erros, CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<string, IReadOnlyList<RegraDerivacao>> regrasPadrao =
            await fatoCandidatoReader.ListarRegrasPadraoAsync(cancellationToken).ConfigureAwait(false);
        Dictionary<string, ConfiguracaoDerivacaoFato> derivados = DerivadosDoProcesso();
        HashSet<string> vistos = new(StringComparer.Ordinal);
        Queue<string> pendentes = new(citados.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
        List<ConfiguracaoDerivacaoInput> aCopiar = [];
        while (pendentes.TryDequeue(out string? codigo))
        {
            if (vistos.Add(codigo) && contexto.Fatos.TryGetValue(codigo, out FatoCandidatoView? fato)
                && VinculoDeFato.Usa(fato.Binding, VinculoDeFato.RegraDeDerivacao))
            {
                Visitar(codigo, derivados, regrasPadrao, pendentes, aCopiar, erros);
            }
        }

        return aCopiar;
    }

    /// <summary>
    /// A modalidade pode vir pela cópia ou pelas regras padrão que ela traz; sem distribuição de vagas, ela
    /// não tem de onde vir.
    /// </summary>
    public void ConferirModalidade(string[] citados, List<ConfiguracaoDerivacaoInput> aCopiar, List<FieldError> erros)
    {
        IEnumerable<string> fechamento = citados.Concat(aCopiar
            .SelectMany(static d => d.Regras).SelectMany(static r => r.Quando ?? []).SelectMany(static c => c).Select(static c => c.Fato));
        if (processo.DistribuicaoVagas.Count == 0
            && fechamento.Any(c => contexto.Fatos.TryGetValue(c, out FatoCandidatoView? fato) && fato.FonteValores == FonteModalidade))
        {
            erros.Add(new("modelo", new DomainError(
                AplicacaoDeModeloErrorCodes.SemDistribuicaoDeVagas,
                "O modelo cita a modalidade de concorrência, que depende da distribuição de vagas do processo; defina a distribuição de vagas antes.")));
        }
    }

    /// <summary>Resolve as derivações a copiar contra o universo de fatos da configuração final.</summary>
    public List<ConfiguracaoDerivacaoFato> Resolver(
        List<ConfiguracaoDerivacaoInput> aCopiar, IReadOnlySet<string> universo, List<FieldError> erros)
    {
        List<ConfiguracaoDerivacaoFato> derivacoes = [];
        foreach (ConfiguracaoDerivacaoInput derivacao in aCopiar)
        {
            Result<ConfiguracaoDerivacaoFato> resolvida = DefinirRegrasDerivacaoCommandHandler.ResolverConfiguracao(
                derivacao, contexto.Fatos, contexto.Vocabulario, universo, contexto.DominiosDinamicos);
            if (resolvida.IsSuccess)
            {
                derivacoes.Add(resolvida.Value!);
            }
            else
            {
                erros.AddRange(resolvida.Errors.Select(e => e with { Field = $"derivacoes[{derivacao.CodigoFato}]" }));
            }
        }

        return derivacoes;
    }

    // Mantém o que o processo grava e o que a oferta deriva. A regra do sistema que as derivações efetivas
    // suprem enquanto o processo não grava outra é copiada como as demais: a cópia não pode depender de a
    // oferta continuar com cota da lei.
    private Dictionary<string, ConfiguracaoDerivacaoFato> DerivadosDoProcesso() =>
        processo.DerivacoesEfetivas()
            .Where(r => DerivacoesDaOferta.Deriva(r.CodigoFato)
                || processo.RegrasDerivacao.Any(g => string.Equals(g.CodigoFato, r.CodigoFato, StringComparison.Ordinal)))
            .ToDictionary(static r => r.CodigoFato, StringComparer.Ordinal);

    private void Visitar(
        string codigo,
        Dictionary<string, ConfiguracaoDerivacaoFato> derivados,
        IReadOnlyDictionary<string, IReadOnlyList<RegraDerivacao>> regrasPadrao,
        Queue<string> pendentes,
        List<ConfiguracaoDerivacaoInput> aCopiar,
        List<FieldError> erros)
    {
        if (derivados.TryGetValue(codigo, out ConfiguracaoDerivacaoFato? derivado))
        {
            relatorio.Mantidas.Add(codigo);
            Enfileirar(pendentes, DerivacoesDaOferta.Deriva(codigo) ? derivado.FatosCitados : []);
        }
        else if (string.Equals(codigo, OpcaoPelasCotasDaLei.Fato, StringComparison.Ordinal))
        {
            erros.Add(Recusa(codigo, AplicacaoDeModeloErrorCodes.SemCotaDaLeiOfertada,
                $"O modelo cita '{codigo}', que só existe no processo que oferta cota da lei; oferte as cotas na distribuição de vagas antes."));
        }
        else if (DerivacoesDaOferta.Deriva(codigo))
        {
            // A modalidade sem modalidade ofertada: a recusa é a da distribuição de vagas que falta.
        }
        else if (regrasPadrao.TryGetValue(codigo, out IReadOnlyList<RegraDerivacao>? regras))
        {
            relatorio.Copiadas.Add(codigo);
            aCopiar.Add(new ConfiguracaoDerivacaoInput(codigo, [.. regras.Select(static (r, ordem) => new RegraDerivacaoInput(ordem, r.Contribui, Quando(r.Quando)))]));
            Enfileirar(pendentes, regras.SelectMany(static r => r.FatosCitados));
        }
        else
        {
            erros.Add(Recusa(codigo, AplicacaoDeModeloErrorCodes.DerivadoSemRegra,
                $"O modelo cita '{codigo}', que o processo ainda não deriva e o catálogo não tem regra padrão; configure as regras de derivação dele no processo antes."));
        }
    }

    private static void Enfileirar(Queue<string> pendentes, IEnumerable<string> dependencias)
    {
        foreach (string dependencia in dependencias)
        {
            pendentes.Enqueue(dependencia);
        }
    }

    private static FieldError Recusa(string codigo, string codigoDoErro, string mensagem) =>
        new($"derivacoes[{codigo}]", new DomainError(codigoDoErro, mensagem));

    // O predicado da regra padrão na forma da entrada de derivação; a regra âncora não tem condição.
    private static IReadOnlyList<IReadOnlyList<CondicaoDerivacaoInput>>? Quando(PredicadoDnf quando) =>
        EntradaDeRegras.ParaEntrada(quando) is { Count: > 0 } clausulas
            ? [.. clausulas.Select(static c => (IReadOnlyList<CondicaoDerivacaoInput>)[.. c.Select(static k => new CondicaoDerivacaoInput(k.Fato, k.Operador, k.Valor))])]
            : null;
}
