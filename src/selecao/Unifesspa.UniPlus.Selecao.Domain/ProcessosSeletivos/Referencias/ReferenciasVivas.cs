namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Referencias;

using System.Text.Json;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public static class ReferenciasVivas
{
    /// <summary>
    /// Os fatos do catálogo que a configuração usa — coletados, derivados por regra ou citados — e
    /// os valores citados ou contribuídos: o vínculo existente que a desativação de um fato ou valor
    /// preserva.
    /// </summary>
    public static VinculosDeFatos Vinculos(EstadoDaConfiguracao estado)
    {
        ArgumentNullException.ThrowIfNull(estado);

        IReadOnlyList<ConfiguracaoDerivacaoFato> derivacoes = estado.DerivacoesEfetivas();
        return VinculosDeFatos.De(
            estado.Campos.Select(static f => f.FatoCodigo).Concat(derivacoes.Select(static r => r.CodigoFato)),
            CondicoesVivas(estado),
            derivacoes.SelectMany(static c => c.CodigosContribuidos));
    }

    /// <summary>
    /// Todo predicado vivo que cita fato por valor: gatilhos de exigência, regras dos itens,
    /// condições das regras de derivação, critérios de desempate por fato, condições dos termos e
    /// exibição das seções do formulário.
    /// </summary>
    internal static IEnumerable<(string Fato, JsonElement Valor)> CondicoesVivas(EstadoDaConfiguracao estado) =>
        CondicoesVivasComOperador(estado).Select(static c => (c.Fato, c.Valor));

    /// <summary>Todo predicado vivo que cita fato, com o operador de cada condição — a enumeração única das fontes.</summary>
    internal static IEnumerable<(string Fato, Operador Operador, JsonElement Valor)> CondicoesVivasComOperador(EstadoDaConfiguracao estado) =>
        estado.DocumentosExigidos.SelectMany(static d => d.Condicoes).Select(static c => (c.Fato, c.Operador, c.Valor))
            .Concat(estado.Itens.SelectMany(static f => f.Condicoes).Select(static c => (c.Fato, c.Operador, c.Valor)))
            .Concat(estado.GruposColetados.SelectMany(static g => g.Condicoes).Select(static c => (c.Fato, c.Operador, c.Valor)))
            .Concat(estado.DerivacoesEfetivas().SelectMany(static r => r.Regras).SelectMany(static r => r.Condicoes)
                .Select(static c => (c.Fato, c.Operador, c.Valor)))
            .Concat(estado.CriteriosDesempate.Select(static c => c.Args).OfType<ArgsDesempatePredicadoFato>()
                .Select(static a => (a.Condicao.Fato, a.Condicao.Operador, a.Condicao.Valor)))
            .Concat(estado.TermosExigidos.SelectMany(static t => t.Condicoes).Select(static c => (c.Fato, c.Operador, c.Valor)))
            .Concat(estado.Formularios.SelectMany(static f => f.Etapas).SelectMany(static e => e.Condicoes).Select(static c => (c.Fato, c.Operador, c.Valor)));

    /// <summary>
    /// Um gatilho DNF sobre um
    /// fato categórico dinâmico (<c>MODALIDADE</c>, <c>CONDICAO_ATENDIMENTO</c>) referencia
    /// um valor por CÓDIGO — nunca por Guid, diferente de <c>FaseCronograma</c>. Isso
    /// permite checagem precisa (não o guard coarse de <c>FaseCronograma.ReferenciadaPorExigenciaViva</c>):
    /// só invalida quando o novo conjunto de códigos ofertados realmente deixaria de conter
    /// um valor hoje referenciado por uma condição viva — redefinir preservando (ou
    /// ampliando) a oferta é sempre aceito.
    /// </summary>
    internal static bool ReferenciaDinamicaSeriaInvalidada(EstadoDaConfiguracao estado, string fato, HashSet<string> novosCodigosValidos)
    {
        // Uma opção removida que qualquer predicado vivo ainda cita faria o edital congelar uma
        // condição que nunca se satisfaz.
        foreach ((string fatoCitado, JsonElement valor) in CondicoesVivas(estado))
        {
            if (!string.Equals(fatoCitado, fato, StringComparison.Ordinal))
            {
                continue;
            }

            IEnumerable<string?> valoresReferenciados = valor.ValueKind == JsonValueKind.Array
                ? valor.EnumerateArray().Select(static v => v.GetString())
                : [valor.ValueKind == JsonValueKind.String ? valor.GetString() : null];

            if (valoresReferenciados.Any(v => v is not null && !novosCodigosValidos.Contains(v)))
            {
                return true;
            }
        }

        return false;
    }
}
