namespace Unifesspa.UniPlus.Regras.Formularios;

using System.Text.Json;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;

internal sealed class ResolucaoDoFormulario
{
    private readonly DefinicaoFormulario _definicao;
    private readonly EntradaAvaliacaoFormulario _entrada;
    private readonly DateOnly? _diaDoEnvio;
    private readonly Dictionary<string, (DefinicaoPagina Pagina, DefinicaoItem Item)> _itens;
    private readonly Dictionary<string, (DefinicaoPagina Pagina, DefinicaoGrupo Grupo)> _grupos;
    private readonly Dictionary<string, DerivacaoDoFato> _derivacoes;
    private readonly Dictionary<string, DefinicaoAgregado> _agregados;
    private readonly Dictionary<string, FatoResolvido> _fatos;
    private readonly Dictionary<string, AvaliacaoItem> _avaliacoesDosItens = new(StringComparer.Ordinal);
    private readonly Dictionary<string, AvaliacaoGrupo> _avaliacoesDosGrupos = new(StringComparer.Ordinal);

    internal ResolucaoDoFormulario(DefinicaoFormulario definicao, EntradaAvaliacaoFormulario entrada)
    {
        _definicao = definicao;
        _entrada = entrada;
        _diaDoEnvio = entrada.Envio?.Dia;
        _itens = definicao.ItensDasPaginas().ToDictionary(static par => par.Item.FatoCodigo, StringComparer.Ordinal);
        _grupos = definicao.GruposDasPaginas().ToDictionary(static par => par.Grupo.Codigo, StringComparer.Ordinal);
        _derivacoes = definicao.Derivacoes.ToDictionary(static derivacao => derivacao.CodigoFato, StringComparer.Ordinal);
        _agregados = definicao.Agregados.ToDictionary(static agregado => agregado.Codigo, StringComparer.Ordinal);
        _fatos = new(entrada.FatosConhecidos, StringComparer.Ordinal);
    }

    internal void ResolverNaOrdem(OrdemTopologica ordem)
    {
        foreach (string codigo in ordem.Ordem)
        {
            Resolver(codigo);
        }
        foreach (string codigo in ordem.ForaDeOrdem)
        {
            DeixarIndeterminado(codigo);
        }
    }

    private void Resolver(string codigo)
    {
        if (_derivacoes.TryGetValue(codigo, out DerivacaoDoFato? derivacao))
        {
            _fatos[codigo] = AvaliadorFormulario.Derivar(derivacao, _fatos);
        }
        else if (_agregados.TryGetValue(codigo, out DefinicaoAgregado? agregado))
        {
            _fatos[codigo] = AgregadoDeGrupo.Calcular(_avaliacoesDosGrupos[agregado.GrupoCodigo], agregado.FatoDeMembro, agregado.Operacao);
        }
        else
        {
            ResolverColeta(codigo);
        }
    }

    private void ResolverColeta(string codigo)
    {
        if (_grupos.TryGetValue(codigo, out (DefinicaoPagina Pagina, DefinicaoGrupo Grupo) grupo))
        {
            _avaliacoesDosGrupos[codigo] = new AvaliacaoDaColeta(grupo.Pagina.Codigo, _diaDoEnvio, _fatos)
                .AvaliarGrupo(grupo.Pagina, grupo.Grupo, _entrada);
        }
        else
        {
            ResolverItem(codigo);
        }
    }

    private void ResolverItem(string codigo)
    {
        (DefinicaoPagina pagina, DefinicaoItem item) = _itens[codigo];
        Ternario visivel = pagina.Exibicao?.Avaliar(_fatos) ?? Ternario.Verdadeiro;
        JsonElement? resposta = _entrada.Respostas.TryGetValue(item.FatoCodigo, out JsonElement valor) ? valor : null;
        AvaliacaoDoItem avaliacaoDoItem = new(pagina.Codigo, _entrada.PaginasConcluidas.Contains(pagina.Codigo), _diaDoEnvio, _fatos, item, visivel);
        (FatoResolvido fato, AvaliacaoItem avaliacao) = avaliacaoDoItem.Avaliar(resposta);
        _fatos[codigo] = fato;
        _avaliacoesDosItens[codigo] = avaliacao;
    }

    internal void DeixarIndeterminado(string codigo)
    {
        if (_grupos.TryGetValue(codigo, out (DefinicaoPagina Pagina, DefinicaoGrupo Grupo) grupo))
        {
            _avaliacoesDosGrupos[codigo] = new AvaliacaoGrupo(codigo, grupo.Pagina.Codigo, Ternario.Indeterminado,
                Ternario.Indeterminado, EstadoFato.Indeterminado, ContagemValida: true, OcorrenciaDoCandidatoValida: true, []);
            return;
        }

        _fatos[codigo] = FatoResolvido.Indeterminado();
        if (_itens.TryGetValue(codigo, out (DefinicaoPagina Pagina, DefinicaoItem Item) item))
        {
            _avaliacoesDosItens[codigo] = new AvaliacaoItem(codigo, item.Pagina.Codigo, Ternario.Indeterminado,
                Ternario.Indeterminado, [], item.Item.Impedimento is null ? Ternario.Falso : Ternario.Indeterminado);
        }
    }

    internal AvaliacaoFormulario Resultado() => new(
        _fatos,
        [.. _definicao.ItensDasPaginas().Select(par => _avaliacoesDosItens[par.Item.FatoCodigo])],
        [.. _definicao.Termos.Select(AvaliarTermo)],
        [.. _definicao.GruposDasPaginas().Select(par => _avaliacoesDosGrupos[par.Grupo.Codigo])],
        [.. _definicao.Paginas.Select(pagina => new AvaliacaoPagina(pagina.Codigo, pagina.Exibicao?.Avaliar(_fatos) ?? Ternario.Verdadeiro))]);

    private AvaliacaoTermo AvaliarTermo(DefinicaoTermo termo)
    {
        Ternario visivel = termo.Exibicao?.Avaliar(_fatos) ?? Ternario.Verdadeiro;
        return new AvaliacaoTermo(termo.Codigo, visivel, AvaliadorFormulario.ObrigatorioSeVisivel(visivel, termo.Obrigatoriedade, _fatos));
    }
}
