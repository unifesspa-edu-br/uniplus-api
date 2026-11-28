namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;

using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>Os itens do painel sobre os formulários, na ordem do gate.</summary>
internal static class ItensDosFormularios
{
    internal static IReadOnlyList<ItemConformidade> Avaliar(ConfiguracaoDeclarada configuracao) =>
    [
        new ItemConformidade("termo_exigido_sem_forma_de_aceite", DimensaoConformidade.ColetaDeFatos, "Termos do formulário: toda versão escolhida tem forma de aceite definida", PendenciasDosFormularios.PendenciaDeTermoSemFormaDeAceite(configuracao) is null),
        new ItemConformidade("formulario_inscricao_ausente", DimensaoConformidade.ColetaDeFatos, "Formulários: a inscrição pelo sistema tem formulário obrigatório na fase que coleta inscrição", PendenciasDosFormularios.PendenciasDaInscricao(configuracao).Count == 0),
        new ItemConformidade("formulario_inscricao_sem_conjunto_basico", DimensaoConformidade.ColetaDeFatos, "Formulários: os da inscrição coletam o conjunto básico de dados do candidato", PendenciasDosFormularios.PendenciaDoConjuntoBasico(configuracao) is null),
        new ItemConformidade("formulario_inscricao_sem_edicao_enem", DimensaoConformidade.ColetaDeFatos, "Formulários: com classificação baseada em ENEM, os da inscrição pedem a edição do ENEM a todo candidato", PendenciasDosFormularios.PendenciaDaEdicaoDoEnem(configuracao) is null),
        new ItemConformidade("formulario_fase_incoerente", DimensaoConformidade.ColetaDeFatos, "Formulários: cada um na fase do cronograma, com as páginas que a fase pede", PendenciasDosFormularios.PendenciaDaFaseDosFormularios(configuracao) is null),
        new ItemConformidade("formulario_isencao_sem_taxa", DimensaoConformidade.ColetaDeFatos, "Formulários: pedido de isenção da taxa só em processo que cobra taxa, e o que cobra tem ao menos um", PendenciasDosFormularios.PendenciasDaIsencao(configuracao).Count == 0),
        new ItemConformidade("formulario_chamadas_sem_obrigatorio", DimensaoConformidade.ColetaDeFatos, "Formulários: toda fase organizada em chamadas tem ao menos um formulário obrigatório", PendenciasDosFormularios.PendenciasDasChamadas(configuracao).Count == 0),
        new ItemConformidade("formulario_pos_importacao_sem_obrigatorio", DimensaoConformidade.ColetaDeFatos, "Formulários: no resultado importado, a primeira fase depois da importação tem formulário obrigatório", PendenciasDosFormularios.PendenciasDaPrimeiraFaseDepoisDaImportacao(configuracao).Count == 0),
        new ItemConformidade("formulario_item_fora_de_secao", DimensaoConformidade.ColetaDeFatos, "Formulários: todo item numa seção, na ordem das seções", PendenciasDosFormularios.PendenciaDosItensForaDeSecao(configuracao) is null),
        new ItemConformidade("formulario_itens_em_excesso", DimensaoConformidade.ColetaDeFatos, $"Formulários: no máximo {FormaDoItem.MaximoDeItens} itens cada", PendenciasDosFormularios.PendenciaDosItensEmExcesso(configuracao) is null),
    ];
}
