namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

internal sealed record EstadoFinalDaCopia(
    ConfiguracaoDeclarada configuracao, CopiaDeModeloDeFormulario copia, HashSet<string> daCopia)
{
    private FatoColetado[] Itens =>
        [.. configuracao.Coleta.Itens.Where(f => f.Finalidade != copia.Finalidade && !daCopia.Contains(f.FatoCodigo)), .. copia.Itens];

    private IReadOnlyList<ConfiguracaoDerivacaoFato> Derivacoes => DerivacoesDaOferta.Efetivas(
        [.. configuracao.Coleta.RegrasDerivacao, .. copia.DerivacoesNovas], configuracao.Vagas.DistribuicaoVagas);

    private TermoExigidoFormulario[] Termos =>
        [.. configuracao.Coleta.TermosExigidos.Where(t => t.Finalidade != copia.Finalidade), .. copia.Termos];

    private GrupoColetado[] Grupos =>
        [.. configuracao.Coleta.GruposColetados.Where(g => g.Finalidade != copia.Finalidade), .. copia.Grupos];

    private Dictionary<string, IReadOnlyCollection<string>> Dependencias => CitacoesDosFormularios.MapaDeDerivacoes(Derivacoes);

    public IReadOnlyList<FieldError> Conferir(FormularioProcesso novo)
    {
        HashSet<string> daInscricao = copia.Finalidade == FinalidadeFormulario.Inscricao
            ? new(StringComparer.Ordinal) : CitacoesDosFormularios.FatosDaInscricao(Itens);
        if (CitacoesDosFormularios.ValidarGrafoDeFatos(novo.Paginas, copia.Itens, copia.Grupos, daInscricao, Dependencias) is { } grafo)
            return [new("itens", grafo)];
        List<FieldError> erros = FormaDoTermo.ConferirUnicidade([.. copia.Termos.Select(static t => ((string?, int)?)(t.Codigo, t.Ordem))]);
        DependenciasDoFormulario dependencias = CitacoesDosFormularios.DependenciasDe(copia.Finalidade, copia.Itens,
            CitacoesDosFormularios.FatosDaInscricao(Itens), Dependencias);
        erros.AddRange(CitacoesDosFormularios.RecusasDosTermos(copia.Termos, dependencias));
        return erros.Count > 0 ? erros : CitacoesOrfas(novo);
    }

    private FieldError[] CitacoesOrfas(FormularioProcesso novo)
    {
        IEnumerable<DomainError> dosOutros = copia.Finalidade == FinalidadeFormulario.Inscricao
            ? CitacoesDosFormularios.CitacoesQueFicariamOrfas(configuracao, daCopia,
                outra => [.. Itens.Where(f => f.Finalidade == outra)], Dependencias) : [];
        return [.. dosOutros.Concat(CitacoesDosFormularios.CitacaoInvalidaNosFormularios([novo], Itens, Termos, Derivacoes, Grupos)
            is { } doNovo ? [doNovo] : []).Select(static recusa => new FieldError(null, recusa))];
    }
}
