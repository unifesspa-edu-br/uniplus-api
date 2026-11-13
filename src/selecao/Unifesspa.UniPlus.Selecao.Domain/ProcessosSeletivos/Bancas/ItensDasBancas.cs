namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>Os itens do painel de conformidade sobre as bancas, cada um projetando o predicado do gate.</summary>
public static class ItensDasBancas
{
    public static IReadOnlyList<ItemConformidade> Avaliar(EstadoDaConfiguracao estado, CorretoresDasEtapas corretores)
    {
        ArgumentNullException.ThrowIfNull(estado);
        ArgumentNullException.ThrowIfNull(corretores);

        return
        [
            Item("cronograma_recurso_com_banca_de_analise", "Cronograma: todo recurso tem a banca de análise de recursos que o julga, na fase ou na etapa", PendenciasDasBancas.PendenciaDoRecursoSemBanca(estado)),
            Item("cronograma_banca_de_recursos_sem_recorte", "Cronograma: a banca de análise de recursos não declara recorte de competência", PendenciasDasBancas.PendenciaDoRecorteEmBancaDeRecursos(estado)),
            Item("cronograma_banca_com_regra_de_deliberacao", "Cronograma: toda banca declarada tem a regra de deliberação", PendenciasDasBancas.PendenciaDaRegraDeDeliberacao(estado)),
            Item("cronograma_banca_com_o_minimo_de_integrantes", "Cronograma: toda banca tem ao menos o mínimo de integrantes que o quórum e os corretores das etapas julgadas exigem", PendenciasDasBancas.PendenciaDoMinimoDeIntegrantes(estado, corretores)),
        ];
    }

    private static ItemConformidade Item(string codigo, string mensagem, DomainError? pendencia) =>
        new(codigo, DimensaoConformidade.Cronograma, mensagem, pendencia is null);
}
