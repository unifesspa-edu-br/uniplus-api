namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;

using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>Os itens do painel sobre os formulários, na ordem do gate.</summary>
internal static class ItensDosFormularios
{
    internal static IReadOnlyList<ItemConformidade> Avaliar(EstadoDaConfiguracao estado) =>
    [
        new ItemConformidade("termo_exigido_sem_forma_de_aceite", DimensaoConformidade.ColetaDeFatos, "Termos do formulário: toda versão escolhida tem forma de aceite definida", PendenciasDosFormularios.PendenciaDeTermoSemFormaDeAceite(estado) is null),
        new ItemConformidade("formulario_inscricao_ausente", DimensaoConformidade.ColetaDeFatos, "Formulários: processo com inscrição própria tem formulário de inscrição", PendenciasDosFormularios.PendenciaDoFormularioDeInscricao(estado) is null),
        new ItemConformidade("formulario_inscricao_sem_conjunto_basico", DimensaoConformidade.ColetaDeFatos, "Formulários: o de inscrição coleta o conjunto básico de dados do candidato", PendenciasDosFormularios.PendenciaDoConjuntoBasico(estado) is null),
        new ItemConformidade("formulario_inscricao_sem_edicao_enem", DimensaoConformidade.ColetaDeFatos, "Formulários: com classificação baseada em ENEM, o de inscrição pede a edição do ENEM a todo candidato", PendenciasDosFormularios.PendenciaDaEdicaoDoEnem(estado) is null),
        new ItemConformidade("formulario_fase_incoerente", DimensaoConformidade.ColetaDeFatos, "Formulários: cada um na fase do cronograma que a finalidade pede", PendenciasDosFormularios.PendenciaDaFaseDosFormularios(estado) is null),
        new ItemConformidade("formulario_isencao_sem_taxa", DimensaoConformidade.ColetaDeFatos, "Formulários: isenção de taxa só em processo que cobra taxa", PendenciasDosFormularios.PendenciaDaIsencaoSemTaxa(estado) is null),
        new ItemConformidade("formulario_item_fora_de_secao", DimensaoConformidade.ColetaDeFatos, "Formulários: todo item numa seção, na ordem das seções", PendenciasDosFormularios.PendenciaDosItensForaDeSecao(estado) is null),
        new ItemConformidade("formulario_itens_em_excesso", DimensaoConformidade.ColetaDeFatos, $"Formulários: no máximo {FormaDoItem.MaximoDeItens} itens cada", PendenciasDosFormularios.PendenciaDosItensEmExcesso(estado) is null),
    ];
}
