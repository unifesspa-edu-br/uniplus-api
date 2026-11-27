namespace Unifesspa.UniPlus.Regras.Formularios;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Leituras;

/// <summary>
/// A forma do documento que chega de fora, conferida em um só percurso, na ordem em que o formulário portável declara os
/// campos. O arquivo importado pode trazer elemento nulo e grupo sem ocorrências, que a conversão não consegue ler sem
/// lançar exceção; por isso, havendo qualquer recusa de forma, só as de forma voltam e nada é convertido. No grupo, as
/// ocorrências são conferidas antes das regras e dos subitens.
/// </summary>
internal sealed class FormaDoPortavel
{
    private readonly List<FieldError> _recusas = [];

    private FormaDoPortavel()
    {
    }

    /// <summary>Todas as recusas de forma do documento, cada uma no caminho do elemento recusado.</summary>
    internal static IReadOnlyList<FieldError> Recusas(FormularioPortavel portavel)
    {
        FormaDoPortavel forma = new();
        forma.Lista(portavel.Paginas, "paginas", forma.Pagina);
        forma.Lista(portavel.Termos, "termos", (termo, caminho) => forma.ExibicaoEObrigatoriedade(termo.Regras, $"{caminho}.regras"));
        forma.Lista(portavel.Derivacoes, "derivacoes", forma.Derivacao);
        forma.Lista(portavel.Agregados, "agregados", static (_, _) => { });
        return forma._recusas;
    }

    private void Pagina(PaginaPortavel pagina, string caminho)
    {
        Predicado(pagina.Exibicao, $"{caminho}.exibicao");
        Lista(pagina.Itens, $"{caminho}.itens", Item);
        Lista(pagina.Grupos, $"{caminho}.grupos", Grupo);
    }

    private void Grupo(GrupoPortavel grupo, string caminho)
    {
        if (grupo.Ocorrencias is null)
        {
            _recusas.Add(new FieldError($"{caminho}.ocorrencias", new DomainError(
                GrupoFormularioErrorCodes.OcorrenciasObrigatorias, "As ocorrências do grupo são obrigatórias.")));
        }

        ExibicaoEObrigatoriedade(grupo.Regras, $"{caminho}.regras");
        Lista(grupo.Subitens, $"{caminho}.subitens", Item);
    }

    private void Derivacao(DerivacaoPortavel derivacao, string caminho) =>
        Lista(derivacao.Regras, $"{caminho}.regras", (regra, daRegra) => Predicado(regra.Quando, $"{daRegra}.quando"));

    private void Item(ItemPortavel item, string caminho)
    {
        RegrasDoCampo(item.Regras, $"{caminho}.regras");
        Lista(item.Oferta, $"{caminho}.oferta", static (_, _) => { });
    }

    private void ExibicaoEObrigatoriedade(ExibicaoEObrigatoriedadeDto? regras, string caminho)
    {
        Predicado(regras?.Exibicao, $"{caminho}.exibicao");
        Predicado(regras?.Obrigatoriedade?.Predicado, $"{caminho}.obrigatoriedade.predicado");
    }

    private void RegrasDoCampo(RegrasDoCampoDto? regras, string caminho)
    {
        ExibicaoEObrigatoriedade(regras is null ? null : new(regras.Exibicao, regras.Obrigatoriedade), caminho);
        Lista(regras?.Restricoes, $"{caminho}.restricoes", Restricao);
        if (regras?.Impedimento is { } impedimento)
        {
            Predicado(impedimento.Quando, $"{caminho}.impedimento.quando");
        }
    }

    private void Restricao(RestricaoValorDto restricao, string caminho)
    {
        Lista(restricao.Entradas, $"{caminho}.entradas", Entrada);
        Lista(restricao.Fatos, $"{caminho}.fatos", static (_, _) => { });
    }

    private void Entrada(OpcoesCondicionadasDto entrada, string caminho)
    {
        Predicado(entrada.Quando, $"{caminho}.quando");
        Lista(entrada.Valores, $"{caminho}.valores", static (_, _) => { });
    }

    private void Predicado(IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoDto>>? predicado, string caminho) =>
        Lista(predicado, caminho, (clausula, daClausula) => Lista(clausula, daClausula, static (_, _) => { }));

    private void Lista<T>(IReadOnlyList<T>? lista, string caminho, Action<T, string> dentro)
        where T : class
    {
        for (int posicao = 0; posicao < (lista?.Count ?? 0); posicao++)
        {
            string doElemento = $"{caminho}[{posicao}]";
            if (lista![posicao] is { } elemento)
            {
                dentro(elemento, doElemento);
            }
            else
            {
                _recusas.Add(new FieldError(doElemento, new DomainError(
                    FormularioPortavelErrorCodes.EstruturaInvalida, "O elemento das regras é nulo.")));
            }
        }
    }
}
