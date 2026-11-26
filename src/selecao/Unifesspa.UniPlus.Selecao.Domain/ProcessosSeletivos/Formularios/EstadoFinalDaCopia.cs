namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>O estado do processo depois da cópia substituir o conteúdo do formulário existente, conferido antes de qualquer mutação.</summary>
internal sealed record EstadoFinalDaCopia(
    ConfiguracaoDeclarada configuracao, CopiaDeModeloDeFormulario copia, FormularioProcesso existente, HashSet<string> daCopia)
{
    private FatoColetado[] Itens =>
        [.. configuracao.Coleta.Itens.Where(f => f.FormularioId != existente.Id && !daCopia.Contains(f.FatoCodigo)), .. copia.Itens];

    private IReadOnlyList<ConfiguracaoDerivacaoFato> Derivacoes => DerivacoesDaOferta.Efetivas(
        [.. configuracao.Coleta.RegrasDerivacao, .. copia.DerivacoesNovas], configuracao.Vagas.DistribuicaoVagas);

    private Dictionary<string, IReadOnlyCollection<string>> Dependencias => CitacoesDosFormularios.MapaDeDerivacoes(Derivacoes);

    private bool DaInscricao => configuracao.EhDaInscricao(existente.Declarado);

    // O formulário da inscrição cita só os campos dele; o outro cita também os da inscrição, como ficam depois da cópia.
    private HashSet<string> CitaveisDaInscricao => DaInscricao
        ? new(StringComparer.Ordinal)
        : PartesDoFormulario.FatosDosFormularios(Itens, configuracao.IdsDaInscricao());

    public IReadOnlyList<FieldError> Conferir(FormularioProcesso novo)
    {
        HashSet<string> citaveis = CitaveisDaInscricao;
        if (CitacoesDosFormularios.ValidarGrafoDeFatos(novo.Paginas, copia.Itens, copia.Grupos, citaveis, Dependencias) is { } grafo)
            return [new("itens", grafo)];
        List<FieldError> erros = FormaDoTermo.ConferirUnicidade([.. copia.Termos.Select(static t => ((string?, int)?)(t.Codigo, t.Ordem))]);
        DependenciasDoFormulario dependencias = CitacoesDosFormularios.DependenciasDe(copia.Itens, citaveis, Dependencias);
        erros.AddRange(CitacoesDosFormularios.RecusasDosTermos(copia.Termos, dependencias));
        return erros.Count > 0 ? erros : CitacoesOrfas(novo, dependencias);
    }

    private FieldError[] CitacoesOrfas(FormularioProcesso novo, DependenciasDoFormulario dependencias)
    {
        IEnumerable<DomainError> dosOutros = DaInscricao
            ? CitacoesDosFormularios.CitacoesQueFicariamOrfas(
                configuracao, DaInscricaoDepois(), outro => [.. Itens.Where(f => f.FormularioId == outro.Id)], Dependencias)
            : [];
        DomainError? doNovo = CitacoesDosFormularios.CitacaoInvalida(novo.Paginas, copia.Itens, copia.Termos, copia.Grupos, dependencias);
        return [.. dosOutros.Concat(doNovo is null ? [] : [doNovo]).Select(static recusa => new FieldError(null, recusa))];
    }

    // Os itens da cópia substituem os do formulário: os fatos da inscrição depois dela são os dos outros formulários da inscrição e os dela.
    private HashSet<string> DaInscricaoDepois()
    {
        HashSet<string> depois = configuracao.FatosDaInscricaoSem(existente);
        depois.UnionWith(daCopia);
        return depois;
    }
}
