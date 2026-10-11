namespace Unifesspa.UniPlus.Selecao.Domain.Services;

using System.Text.Json;

using Entities;

using Enums;

using Errors;

using Kernel.Results;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// As derivações que a oferta de vagas define, e que o processo não configura: a modalidade do
/// candidato, a união das regras de aplicabilidade das modalidades ofertadas (UNI-REQ-0076,
/// ADR-0144), e a opção pelas cotas da lei, da qual essas regras dependem.
/// </summary>
/// <remarks>
/// <para>
/// Com cota da lei ofertada, a opção é derivada pela regra padrão, de quem é egresso de escola pública
/// e optou pelas vagas de escola pública, sempre que uma regra a cita; o egresso, quando o processo
/// não o configura, vem pela regra do sistema. Sem cota da lei ofertada,
/// ninguém optou por ela: a regra de cada modalidade é lida com a opção valendo falso. A cláusula que exige a opção não vale para ninguém, e a condição que a recusa vale
/// para todos. A modalidade cujas cláusulas exigem todas a opção não é derivada.
/// </para>
/// <para>
/// A modalidade não é gravada à parte: ela é projeção das modalidades ofertadas, e gravá-la seria
/// guardar de novo o que a oferta já diz.
/// </para>
/// </remarks>
public static class DerivacoesDaOferta
{
    /// <summary>Se o fato é derivado pela oferta, e não pela configuração do processo.</summary>
    public static bool Deriva(string codigoFato) =>
        string.Equals(codigoFato, ModalidadeDoCandidato.Fato, StringComparison.Ordinal)
        || string.Equals(codigoFato, OpcaoPelasCotasDaLei.Fato, StringComparison.Ordinal);

    /// <summary>A recusa de configurar no processo um fato que a oferta deriva.</summary>
    public static DomainError RecusaDeConfiguracao(string codigoFato) => new(
        ProcessoSeletivoErrorCodes.DerivacaoDaOferta,
        $"O fato '{codigoFato}' é derivado das modalidades que o processo oferta, e não se configura no processo.");

    /// <summary>
    /// As derivações efetivas: as que o processo configura e, havendo modalidade ofertada, as da
    /// oferta.
    /// </summary>
    public static IReadOnlyList<ConfiguracaoDerivacaoFato> Efetivas(
        IEnumerable<ConfiguracaoDerivacaoFato> configuradas,
        IEnumerable<ConfiguracaoDistribuicaoVagas> distribuicao)
    {
        ArgumentNullException.ThrowIfNull(distribuicao);
        return Efetivas(configuradas, distribuicao.SelectMany(static d => d.Modalidades));
    }

    /// <summary>As derivações efetivas a partir das modalidades ofertadas, de todas as ofertas.</summary>
    public static IReadOnlyList<ConfiguracaoDerivacaoFato> Efetivas(
        IEnumerable<ConfiguracaoDerivacaoFato> configuradas,
        IEnumerable<ModalidadeSelecionada> modalidades)
    {
        ArgumentNullException.ThrowIfNull(configuradas);
        ArgumentNullException.ThrowIfNull(modalidades);

        List<ConfiguracaoDerivacaoFato> efetivas = [.. configuradas];
        List<ModalidadeSelecionada> ofertadas = [.. modalidades];
        bool ofertaCotaDaLei = ofertadas.Exists(static m => m.NaturezaLegal == NaturezaLegalModalidade.CotaReservada);
        List<(string? Contribui, PredicadoDnf Quando)> regras =
            [.. RegrasNaOferta(ofertadas).Select(static r => ((string?)r.Modalidade.Codigo, r.Regra))];
        if (regras.Count == 0)
        {
            return efetivas;
        }

        if (ofertaCotaDaLei && regras.Exists(static r => r.Quando.FatosCitados.Contains(OpcaoPelasCotasDaLei.Fato)))
        {
            efetivas.Add(Derivacao(OpcaoPelasCotasDaLei.Fato, [.. OpcaoPelasCotasDaLei.RegrasDaOpcao.Select(static r => (r.Contribui, r.Quando))]));

            // A opção depende do egresso de escola pública. A regra dele é a do sistema, fixa no código
            // e igual à padrão do catálogo, por isso vale sem cópia quando o processo não configura
            // outra. A que o processo configura, vinda do modelo ou do operador, prevalece.
            if (!efetivas.Exists(static d => string.Equals(d.CodigoFato, OrigemEscolar.FatoEgresso, StringComparison.Ordinal)))
            {
                efetivas.Add(Derivacao(OrigemEscolar.FatoEgresso, [.. OrigemEscolar.RegrasDoEgresso.Select(static r => (r.Contribui, r.Quando))]));
            }
        }

        efetivas.Add(Derivacao(ModalidadeDoCandidato.Fato, regras));
        return efetivas;
    }

    /// <summary>
    /// A regra de cada modalidade ofertada como ela vale na oferta — lida com a opção pelas cotas
    /// valendo falso quando nenhuma cota da lei é ofertada —, uma por código, na ordem do código. A
    /// modalidade que não vale para ninguém nessa oferta fica de fora.
    /// </summary>
    public static IReadOnlyList<(ModalidadeSelecionada Modalidade, PredicadoDnf Regra)> RegrasNaOferta(IEnumerable<ModalidadeSelecionada> modalidades)
    {
        ArgumentNullException.ThrowIfNull(modalidades);

        List<ModalidadeSelecionada> ofertadas = [.. modalidades
            .DistinctBy(static m => m.Codigo, StringComparer.Ordinal)
            .OrderBy(static m => m.Codigo, StringComparer.Ordinal)];
        bool ofertaCotaDaLei = ofertadas.Exists(static m => m.NaturezaLegal == NaturezaLegalModalidade.CotaReservada);
        List<(ModalidadeSelecionada, PredicadoDnf)> regras = [];
        foreach (ModalidadeSelecionada modalidade in ofertadas)
        {
            PredicadoDnf? regra = ofertaCotaDaLei ? modalidade.RegraDeAplicabilidade : SemOpcaoPelasCotas(modalidade.RegraDeAplicabilidade);
            if (regra is not null)
            {
                regras.Add((modalidade, regra));
            }
        }

        return regras;
    }

    /// <summary>
    /// A regra lida com a opção pelas cotas valendo falso; nula quando nenhuma cláusula vale assim.
    /// A condição sobre a opção que não é igualdade ou diferença com booleano fica na cláusula: o
    /// processo não deriva a opção, e a publicação recusa a regra que a cita.
    /// </summary>
    private static PredicadoDnf? SemOpcaoPelasCotas(PredicadoDnf regra)
    {
        if (regra.Clausulas.Count == 0)
        {
            return regra;
        }

        List<(int Clausula, CondicaoDnf Condicao)> linhas = [];
        int indice = 0;
        foreach (ClausulaDnf clausula in regra.Clausulas)
        {
            List<CondicaoDnf> restantes = [];
            bool vale = true;
            foreach (CondicaoDnf condicao in clausula.Condicoes)
            {
                switch (ComOpcaoFalsa(condicao))
                {
                    case true:
                        break;
                    case false:
                        vale = false;
                        break;
                    default:
                        restantes.Add(condicao);
                        break;
                }
            }

            if (!vale)
            {
                continue;
            }

            if (restantes.Count == 0)
            {
                return PredicadoDnf.SemClausula;
            }

            linhas.AddRange(restantes.Select(c => (indice, c)));
            indice++;
        }

        return indice == 0 ? null : PredicadoDnf.CriarDeCondicoesAgrupadas(linhas).Value!;
    }

    /// <summary>O valor da condição sobre a opção pelas cotas valendo falso; nulo na que não é sobre ela.</summary>
    private static bool? ComOpcaoFalsa(CondicaoDnf condicao)
    {
        if (!string.Equals(condicao.Fato, OpcaoPelasCotasDaLei.Fato, StringComparison.Ordinal)
            || condicao.Valor.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            return null;
        }

        bool exigeVerdadeiro = condicao.Valor.ValueKind == JsonValueKind.True;
        return condicao.Operador switch
        {
            Operador.Igual => !exigeVerdadeiro,
            Operador.Diferente => exigeVerdadeiro,
            _ => null,
        };
    }

    private static ConfiguracaoDerivacaoFato Derivacao(string codigoFato, IReadOnlyList<(string? Contribui, PredicadoDnf Quando)> regras) =>
        ConfiguracaoDerivacaoFato.Criar(
            codigoFato,
            [.. regras.Select(static (regra, ordem) => RegraDerivacaoConfigurada.Criar(
                ordem,
                regra.Contribui,
                [.. regra.Quando.Clausulas.SelectMany(static (clausula, indice) => clausula.Condicoes.Select(c =>
                    CondicaoRegraDerivacao.Criar(indice, c.Fato, c.Operador, c.Valor).Value!))]).Value!)]).Value!;
}
