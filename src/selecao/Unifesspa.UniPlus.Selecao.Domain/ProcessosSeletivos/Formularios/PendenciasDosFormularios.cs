namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Classificacao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Coleta;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Desempate;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Exigencias;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Fatos;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Opcoes;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Prazos;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Retificacao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public static class PendenciasDosFormularios
{
    /// <summary>
    /// O fato que os formulários da inscrição já coletavam continua coletado por eles, no mesmo papel: quem se inscreveu
    /// numa versão anterior o informou, e os outros formulários e as versões seguintes contam com ele (UNI-REQ-0144).
    /// Sem versão publicada, nada foi coletado ainda.
    /// </summary>
    internal static DomainError? PendenciaDeFatoJaColetadoRemovido(ConfiguracaoDeclarada configuracao, FatosDasVersoesPublicadas? publicadas)
    {
        IReadOnlySet<Guid> daInscricao = configuracao.IdsDaInscricao();
        return publicadas?.PrimeiroDaInscricaoQueDeixaDeSerColetado(
            configuracao.Coleta.Itens.Where(f => f.FormularioId is { } id && daInscricao.Contains(id)),
            configuracao.Coleta.GruposColetados.Where(g => g.FormularioId is { } id && daInscricao.Contains(id))) is { } retirado
            ? new DomainError(
                RetificacaoErrorCodes.FatoJaColetadoRemovido,
                $"O fato '{retirado}' já é coletado pelos formulários da inscrição do edital publicado: a retificação não o retira deles nem o passa de item para campo de grupo, ou de um grupo para outro.")
            : null;
    }

    /// <summary>
    /// A obrigatoriedade dos formulários, conferida como a regra de formulários por fase a declara (UNI-REQ-0144):
    /// a inscrição pelo sistema tem ao menos um formulário obrigatório na fase que coleta inscrição, e o
    /// resultado importado não tem formulário nela.
    /// </summary>
    internal static IReadOnlyList<DomainError> PendenciasDaInscricao(ConfiguracaoDeclarada configuracao) =>
        [.. RecusasDaObrigatoriedade(configuracao).Where(static recusa => CodigosDaInscricao.Contains(recusa.Code))];

    /// <summary>
    /// A janela do pedido de isenção, conferida como a regra de formulários por fase a declara (UNI-REQ-0144):
    /// o formulário da janela é facultativo, só o processo que cobra taxa o tem e o que cobra taxa tem ao menos um.
    /// </summary>
    internal static IReadOnlyList<DomainError> PendenciasDaIsencao(ConfiguracaoDeclarada configuracao) =>
        [.. RecusasDaObrigatoriedade(configuracao).Where(static recusa => !CodigosDaInscricao.Contains(recusa.Code))];

    private static readonly string[] CodigosDaInscricao =
    [
        FormularioProcessoErrorCodes.InscricaoSemFormularioObrigatorio,
        FormularioProcessoErrorCodes.InscricaoEmResultadoImportado,
    ];

    private static IEnumerable<DomainError> RecusasDaObrigatoriedade(ConfiguracaoDeclarada configuracao) =>
        ObrigatoriedadeDosFormularios.Conferir(
            ProcessoDosFormularios.De(configuracao),
            [.. configuracao.Coleta.Formularios
                .OrderBy(static formulario => formulario.Codigo, CodigoDoFormulario.OrdemOrdinal)
                .Select(formulario => ColetaDoFormulario.De(configuracao, formulario))],
            configuracao.Cronograma.CronogramaFases).Select(static recusa => recusa.Error);

    /// <summary>
    /// Os formulários da inscrição coletam, juntos, todo o conjunto básico de dados do candidato, na seção
    /// reservada: a escrita o garante, e a publicação recusa o conjunto a que falta algum dado básico nela.
    /// </summary>
    internal static DomainError? PendenciaDoConjuntoBasico(ConfiguracaoDeclarada configuracao)
    {
        if (configuracao.DaInscricao().Count == 0)
        {
            return null;
        }

        HashSet<string> coletados = FatosColetadosNaSecaoDosDadosBasicos(configuracao);
        int faltam = ConjuntoBasicoDaInscricao.Fatos.Count(f => !coletados.Contains(f));
        return faltam == 0
            ? null
            : new DomainError(
                FormularioProcessoErrorCodes.InscricaoSemConjuntoBasico,
                $"Os formulários da inscrição não coletam {faltam} dos dados básicos do candidato. Grave de novo o formulário e, depois, os itens dele: a gravação repõe a seção dos dados básicos.");
    }

    private static HashSet<string> FatosColetadosNaSecaoDosDadosBasicos(ConfiguracaoDeclarada configuracao)
    {
        IReadOnlySet<Guid> daInscricao = configuracao.IdsDaInscricao();
        return new(
            configuracao.Coleta.Itens.Where(f => f.FormularioId is { } id && daInscricao.Contains(id)
                    && string.Equals(f.PaginaCodigo, ConjuntoBasicoDaInscricao.CodigoDaSecao, StringComparison.Ordinal))
                .Select(static f => f.FatoCodigo),
            StringComparer.Ordinal);
    }

    /// <summary>
    /// Com inscrição própria e classificação baseada em ENEM, algum formulário da inscrição pede a todo
    /// candidato a edição do ENEM cujas notas a classificação usa: o item é obrigatório sempre, sem exibição
    /// condicional, fora de grupo e numa seção que não se esconde. Sem formulário da inscrição, a falta é a
    /// da obrigatoriedade dos formulários.
    /// </summary>
    internal static DomainError? PendenciaDaEdicaoDoEnem(ConfiguracaoDeclarada configuracao)
    {
        if (configuracao.Identidade.OrigemCandidatos != OrigemCandidatos.InscricaoPropria
            || configuracao.Avaliacao.Classificacao is not { BaseadoEmEnem: true }
            || configuracao.DaInscricao() is not { Count: > 0 } formularios)
        {
            return null;
        }

        return formularios.Any(formulario => PedeAEdicaoDoEnem(configuracao, formulario))
            ? null
            : new DomainError(
                FormularioProcessoErrorCodes.InscricaoSemEdicaoDoEnem,
                "A classificação usa as notas do ENEM, e os formulários da inscrição não pedem a edição do ENEM a todo candidato: o campo da edição é obrigatório sempre, sem exibição condicional e numa seção que não se esconde.");
    }

    private static bool PedeAEdicaoDoEnem(ConfiguracaoDeclarada configuracao, FormularioProcesso formulario) =>
        configuracao.Coleta.ItensDo(formulario).Any(item =>
            string.Equals(item.FatoCodigo, EdicaoDoEnem.Fato, StringComparison.Ordinal) && formulario.PedeATodoCandidato(item));

    /// <summary>
    /// A edição do ENEM só existe com classificação baseada em ENEM: nenhum formulário a coleta fora
    /// disso, nem em grupo. Estática para que o codec confira o envelope pela mesma regra.
    /// </summary>
    public static DomainError? EdicaoDoEnemSemClassificacaoEnem(
        IEnumerable<FatoColetado> campos, ConfiguracaoClassificacao? classificacao)
    {
        ArgumentNullException.ThrowIfNull(campos);

        return classificacao is not { BaseadoEmEnem: true }
            && campos.Any(static f => string.Equals(f.FatoCodigo, EdicaoDoEnem.Fato, StringComparison.Ordinal))
            ? new DomainError(
                FormularioProcessoErrorCodes.EdicaoDoEnemSemClassificacaoEnem,
                "Um formulário pede a edição do ENEM, mas a classificação do processo não usa as notas do ENEM: retire o campo da edição ou declare a classificação baseada em ENEM.")
            : null;
    }

    /// <summary>
    /// Todo formulário tem a fase em que é respondido, e ela continua no cronograma — a definição confere quando
    /// a fase é informada, e o cronograma pode ter mudado depois. A estrutura das páginas se confere de novo contra
    /// o cronograma de hoje: o formulário que serve à fase de inscrição tem as seções que ela pede.
    /// </summary>
    internal static DomainError? PendenciaDaFaseDosFormularios(ConfiguracaoDeclarada configuracao) =>
        FormulariosEmOrdem(configuracao)
            .Select(formulario => RecusaDaFaseFaltanteOuForaDaRegra(configuracao, formulario) ?? RecusaDaEstruturaDasPaginas(configuracao, formulario))
            .FirstOrDefault(static recusa => recusa is not null);

    private static DomainError? RecusaDaFaseFaltanteOuForaDaRegra(ConfiguracaoDeclarada configuracao, FormularioProcesso formulario) =>
        formulario.FaseId is not { } fase
            ? new DomainError(
                FormularioProcessoErrorCodes.SemFase,
                "O formulário ainda não declara a fase em que é respondido.")
            : RegrasDosFormularios.RecusaDaFaseForaDoCronograma(configuracao, fase);

    private static DomainError? RecusaDaEstruturaDasPaginas(ConfiguracaoDeclarada configuracao, FormularioProcesso formulario) =>
        EstruturaFormulario.ValidarPaginas(configuracao.Estrutura(formulario.Declarado), formulario.Estrutura) is [{ } primeira, ..]
            ? primeira.Error
            : null;

    private static IOrderedEnumerable<FormularioProcesso> FormulariosEmOrdem(ConfiguracaoDeclarada configuracao) =>
        configuracao.Coleta.Formularios.OrderBy(static formulario => formulario.Codigo, CodigoDoFormulario.OrdemOrdinal);

    /// <summary>Todo item publicado está numa seção do seu formulário, na ordem das seções.</summary>
    internal static DomainError? PendenciaDosItensForaDeSecao(ConfiguracaoDeclarada configuracao)
    {
        foreach (FormularioProcesso formulario in FormulariosEmOrdem(configuracao))
        {
            if (EstruturaFormulario.ValidarItens(formulario.Estrutura, RegrasDosFormularios.ItensDoFormulario(configuracao, formulario)) is [{ } primeiro, ..])
            {
                return new DomainError(FormularioProcessoErrorCodes.ItemForaDeSecao, primeiro.Error.Message);
            }
        }

        return null;
    }

    /// <summary>
    /// O mesmo teto da gravação, conferido de novo na publicação: uma gravação feita fora do
    /// agregado não publica formulário que o PUT seguinte da mesma lista recusaria.
    /// </summary>
    internal static DomainError? PendenciaDosItensEmExcesso(ConfiguracaoDeclarada configuracao) =>
        FormulariosEmOrdem(configuracao)
            .Select(formulario => FormaDoItem.ValidarQuantidade(CitacoesDosFormularios.QuantidadeNoTeto(
                configuracao.Coleta.ItensDo(formulario).Count(), configuracao.Coleta.GruposDo(formulario))))
            .FirstOrDefault(static erros => erros.Count > 0) is [var excesso, ..]
            ? excesso.Error
            : null;

    /// <summary>
    /// Termo exigido cuja versão ainda não definiu a forma de aceite não é publicável
    /// (UNI-REQ-0086): só o registro digital, com ou sem o endereço IP, é forma resolvida.
    /// </summary>
    internal static DomainError? PendenciaDeTermoSemFormaDeAceite(ConfiguracaoDeclarada configuracao) =>
        configuracao.Coleta.TermosExigidos.OrderBy(static t => t.Ordem).FirstOrDefault(static t => t.SemFormaDeAceite) is { } termo
            ? new DomainError(
                TermoExigidoFormularioErrorCodes.SemFormaDeAceite,
                "A versão do termo exigido não tem forma de aceite definida; escolha uma versão com registro digital.")
            : null;
}
