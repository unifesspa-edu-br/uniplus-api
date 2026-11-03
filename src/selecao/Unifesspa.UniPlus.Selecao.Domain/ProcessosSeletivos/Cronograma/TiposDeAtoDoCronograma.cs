namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;

using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public static class TiposDeAtoDoCronograma
{
    /// <summary>
    /// A primeira pendência de um tipo de ato único por objeto declarado mais de uma vez no
    /// processo — nos produtos das fases e das etapas e, quando há, no ato da própria
    /// operação —, ou <see langword="null"/> quando cada um deles é declarado uma só vez.
    /// </summary>
    /// <remarks>
    /// O tipo único só admite um ato por processo (ADR-0107): declará-lo duas vezes faz o
    /// edital sair publicado, o primeiro ato ser registrado e o segundo ser recusado na fila
    /// semanas depois, com o edital já congelado. O ato da publicação ou da retificação ocupa
    /// a mesma vaga, então um produto do cronograma do mesmo tipo é declaração repetida. Declarar
    /// o mesmo tipo como preliminar e como definitivo conta como duas declarações: o tipo único
    /// é só o ato que sai uma vez no processo, sem preliminar e definitivo, e a matéria que sai
    /// nos dois papéis ou em mais de um ciclo usa tipo não único. Quais tipos são únicos é dado do
    /// catálogo, que chega em <paramref name="tiposUnicos"/> — o agregado não o conhece (ADR-0042).
    /// </remarks>
    public static DomainError? PendenciaDoTipoDeAtoUnico(EstadoDaConfiguracao estado, TiposDeAtoUnicosPorObjeto tiposUnicos)
    {
        ArgumentNullException.ThrowIfNull(tiposUnicos);

        IEnumerable<DeclaracaoDeAto> declaracoes =
        [
            .. DeclaracoesDeAtos(estado).Where(declaracao => tiposUnicos.EhUnico(declaracao.AtoCodigo)),
            .. VagasJaReivindicadas(tiposUnicos),
        ];

        IGrouping<string, DeclaracaoDeAto>? repetido = declaracoes
            .GroupBy(static declaracao => declaracao.AtoCodigo, StringComparer.Ordinal)
            .FirstOrDefault(static grupo => grupo.Count() > 1);

        if (repetido is null)
        {
            return null;
        }

        string onde = string.Join(" e ", repetido
            .GroupBy(static declaracao => declaracao.Lugar, StringComparer.Ordinal)
            .Select(static lugar => lugar.Count() > 1 ? $"{lugar.Key} ({lugar.Count()} vezes)" : lugar.Key));

        return new DomainError(
            ProcessoSeletivoErrorCodes.TipoDeAtoUnicoDeclaradoMaisDeUmaVez,
            $"O tipo de ato '{tiposUnicos.NomeDe(repetido.Key)}' só pode ser publicado uma vez por processo e está declarado {onde}.");
    }

    /// <summary>
    /// O primeiro produto declarado sem papel cujo tipo é resultado no catálogo, ou
    /// <see langword="null"/>. O resultado sem preliminar é declarado definitivo: só o ato que
    /// não é resultado fica sem papel (UNI-REQ-0157).
    /// </summary>
    /// <remarks>
    /// A classificação é dado do catálogo e chega em <paramref name="tipos"/>. Tipo sem versão
    /// vigente não tem classificação e fica de fora: quem o recusa é a conferência do tipo.
    /// </remarks>
    public static DomainError? PendenciaDoResultadoSemPapel(EstadoDaConfiguracao estado, TiposDeAtoUnicosPorObjeto tipos)
    {
        ArgumentNullException.ThrowIfNull(tipos);

        foreach (DeclaracaoDeAto declaracao in DeclaracoesDeAtos(estado))
        {
            if (tipos.ClassificacaoDe(declaracao.AtoCodigo)?.ViolacaoDoResultadoSemPapel(declaracao.AtoCodigo, declaracao.Papel, declaracao.Lugar) is { } violacao)
            {
                return violacao;
            }
        }

        return null;
    }

    /// <summary>
    /// O primeiro produto declarado com papel cujo tipo congela a configuração, ou
    /// <see langword="null"/>. A correção de um resultado não cria versão nova (UNI-REQ-0156),
    /// então um tipo que congela não teria correção possível como item de resultado.
    /// </summary>
    public static DomainError? PendenciaDoTipoQueCongelaDeclaradoComoResultado(EstadoDaConfiguracao estado, TiposDeAtoUnicosPorObjeto tipos)
    {
        ArgumentNullException.ThrowIfNull(tipos);

        foreach (DeclaracaoDeAto declaracao in DeclaracoesDeAtos(estado))
        {
            if (tipos.ClassificacaoDe(declaracao.AtoCodigo)?.ViolacaoDoTipoQueCongelaComPapel(declaracao.AtoCodigo, declaracao.Papel, declaracao.Lugar) is { } violacao)
            {
                return violacao;
            }
        }

        return null;
    }

    /// <summary>
    /// As vagas que a operação e os atos já registrados reivindicam, uma por tipo: o ato da
    /// operação e o ato já registrado do mesmo tipo são a mesma vaga — a retificação continua a
    /// própria linhagem, não disputa com ela —, e por isso contam uma vez só.
    /// </summary>
    internal static IEnumerable<DeclaracaoDeAto> VagasJaReivindicadas(TiposDeAtoUnicosPorObjeto tiposUnicos)
    {
        string? atoDaOperacao = tiposUnicos.AtoDaOperacaoCodigo;

        foreach (string codigo in tiposUnicos.CodigosOcupados)
        {
            bool ocupaPeloAto = tiposUnicos.AtoDaOperacaoEhUnico && string.Equals(codigo, atoDaOperacao, StringComparison.Ordinal);
            if (tiposUnicos.EhUnico(codigo) || ocupaPeloAto)
            {
                yield return new DeclaracaoDeAto(codigo, "no ato já registrado do processo");
            }
        }

        if (atoDaOperacao is not null && tiposUnicos.AtoDaOperacaoEhUnico && !tiposUnicos.VagaOcupada(atoDaOperacao))
        {
            yield return new DeclaracaoDeAto(atoDaOperacao, "no ato que está sendo registrado");
        }
    }

    /// <summary>
    /// Cada produto que o cronograma declara, o das fases e o das etapas, com o lugar que o
    /// declara. É a fonte única dos atos declarados: quem precisa saber quais tipos de ato o
    /// processo cita lê daqui.
    /// </summary>
    /// <remarks>
    /// A etapa é identificada pela ordem além do nome, porque o nome não é único: duas etapas
    /// de mesmo nome em lugares distintos seriam contadas como uma.
    /// </remarks>
    public static IReadOnlyList<DeclaracaoDeAto> DeclaracoesDeAtos(EstadoDaConfiguracao estado)
    {
        ArgumentNullException.ThrowIfNull(estado);

        return [
            .. estado.CronogramaFases.SelectMany(static fase => fase.Produtos
                .Select(produto => new DeclaracaoDeAto(produto.AtoCodigo, $"na fase '{fase.Codigo}'", produto.Papel))),
            .. estado.Etapas.SelectMany(static etapa => etapa.Produtos
                .Select(produto => new DeclaracaoDeAto(produto.AtoCodigo, $"na etapa '{etapa.Nome}' (ordem {etapa.Ordem?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "não definida"})", produto.Papel))),
        ];
    }
}
