namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;

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
    /// O fato que o formulário de inscrição já coletava continua coletado por ela, no mesmo papel: quem se inscreveu
    /// numa versão anterior o informou, e as outras finalidades e as versões seguintes contam com ele (UNI-REQ-0144).
    /// Sem versão publicada, nada foi coletado ainda.
    /// </summary>
    internal static DomainError? PendenciaDeFatoJaColetadoRemovido(ConfiguracaoDeclarada configuracao, FatosDasVersoesPublicadas? publicadas) =>
        publicadas?.PrimeiroDaInscricaoQueDeixaDeSerColetado(
            configuracao.Coleta.Itens.Where(f => f.Finalidade == FinalidadeFormulario.Inscricao),
            CitacoesDosFormularios.GruposDaFinalidade(configuracao, FinalidadeFormulario.Inscricao)) is { } retirado
            ? new DomainError(
                RetificacaoErrorCodes.FatoJaColetadoRemovido,
                $"O fato '{retirado}' já é coletado pelo formulário de inscrição do edital publicado: a retificação não o retira do formulário nem o passa de item para campo de grupo, ou de um grupo para outro.")
            : null;

    /// <summary>
    /// Processo com inscrição própria publica o formulário de inscrição (UNI-REQ-0144): sem ele, o
    /// candidato não tem onde se inscrever.
    /// </summary>
    internal static DomainError? PendenciaDoFormularioDeInscricao(ConfiguracaoDeclarada configuracao) =>
        configuracao.Identidade.OrigemCandidatos == OrigemCandidatos.InscricaoPropria && configuracao.Coleta.FormularioDe(FinalidadeFormulario.Inscricao) is null
            ? new DomainError(FormularioProcessoErrorCodes.InscricaoSemFormulario, "O processo tem inscrição própria e ainda não tem formulário de inscrição.")
            : null;

    /// <summary>
    /// O formulário de inscrição coleta todo o conjunto básico de dados do candidato, na seção
    /// reservada: a escrita o garante, e a publicação recusa o formulário a que falta algum dado
    /// básico nela.
    /// </summary>
    internal static DomainError? PendenciaDoConjuntoBasico(ConfiguracaoDeclarada configuracao)
    {
        if (configuracao.Coleta.FormularioDe(FinalidadeFormulario.Inscricao) is null)
        {
            return null;
        }

        HashSet<string> coletados = FatosColetadosNaSecaoDosDadosBasicos(configuracao);
        int faltam = ConjuntoBasicoDaInscricao.Fatos.Count(f => !coletados.Contains(f));
        return faltam == 0
            ? null
            : new DomainError(
                FormularioProcessoErrorCodes.InscricaoSemConjuntoBasico,
                $"O formulário de inscrição não coleta {faltam} dos dados básicos do candidato. Grave de novo o formulário e, depois, os itens dele: a gravação repõe a seção dos dados básicos.");
    }

    private static HashSet<string> FatosColetadosNaSecaoDosDadosBasicos(ConfiguracaoDeclarada configuracao) => new(
        configuracao.Coleta.Itens.Where(static f => f.Finalidade == FinalidadeFormulario.Inscricao
                && string.Equals(f.EtapaCodigo, ConjuntoBasicoDaInscricao.CodigoDaSecao, StringComparison.Ordinal))
            .Select(static f => f.FatoCodigo),
        StringComparer.Ordinal);

    /// <summary>
    /// Com inscrição própria e classificação baseada em ENEM, o formulário de inscrição pede a todo
    /// candidato a edição do ENEM cujas notas a classificação usa: o item é obrigatório
    /// sempre, sem exibição condicional, fora de grupo e numa seção que não se esconde. Sem formulário
    /// de inscrição, a falta é a do próprio formulário.
    /// </summary>
    internal static DomainError? PendenciaDaEdicaoDoEnem(ConfiguracaoDeclarada configuracao)
    {
        if (configuracao.Identidade.OrigemCandidatos != OrigemCandidatos.InscricaoPropria
            || configuracao.Avaliacao.Classificacao is not { BaseadoEmEnem: true }
            || configuracao.Coleta.FormularioDe(FinalidadeFormulario.Inscricao) is not { } formulario)
        {
            return null;
        }

        FatoColetado? edicao = configuracao.Coleta.Itens.FirstOrDefault(static f => f.Finalidade == FinalidadeFormulario.Inscricao
            && string.Equals(f.FatoCodigo, EdicaoDoEnem.Fato, StringComparison.Ordinal));
        return edicao is not null && formulario.PedeATodoCandidato(edicao)
            ? null
            : new DomainError(
                FormularioProcessoErrorCodes.InscricaoSemEdicaoDoEnem,
                "A classificação usa as notas do ENEM, e o formulário de inscrição não pede a edição do ENEM a todo candidato: o campo da edição é obrigatório sempre, sem exibição condicional e numa seção que não se esconde.");
    }

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
    /// Todo formulário tem a fase que a finalidade pede e que continua no cronograma — a definição
    /// confere quando a fase é informada, e o cronograma pode ter mudado depois.
    /// </summary>
    internal static DomainError? PendenciaDaFaseDosFormularios(ConfiguracaoDeclarada configuracao) =>
        configuracao.Coleta.Formularios.OrderBy(static f => f.Finalidade)
            .Select(formulario => RecusaDaFaseFaltanteOuForaDaRegra(configuracao, formulario))
            .FirstOrDefault(static recusa => recusa is not null);

    private static DomainError? RecusaDaFaseFaltanteOuForaDaRegra(ConfiguracaoDeclarada configuracao, FormularioProcesso formulario) =>
        formulario.FaseId is not { } fase
            ? new DomainError(
                FormularioProcessoErrorCodes.SemFase,
                $"O formulário de {EstruturaFormulario.ParaToken(formulario.Finalidade)} ainda não declara a fase em que é respondido.")
            : RegrasDosFormularios.RecusaDaFaseDoFormulario(configuracao, formulario.Finalidade, fase);

    /// <summary>Formulário de isenção só em processo que cobra taxa de inscrição (UNI-REQ-0144).</summary>
    internal static DomainError? PendenciaDaIsencaoSemTaxa(ConfiguracaoDeclarada configuracao) =>
        configuracao.Coleta.FormularioDe(FinalidadeFormulario.IsencaoTaxa) is not null && configuracao.DivulgacaoETaxa.ConfiguracaoTaxaInscricao?.Cobra != true
            ? new DomainError(FormularioProcessoErrorCodes.IsencaoSemTaxa, "O processo tem formulário de isenção de taxa, mas não cobra taxa de inscrição.")
            : null;

    /// <summary>Todo item publicado está numa seção do seu formulário, na ordem das seções.</summary>
    internal static DomainError? PendenciaDosItensForaDeSecao(ConfiguracaoDeclarada configuracao)
    {
        foreach (FormularioProcesso formulario in configuracao.Coleta.Formularios.OrderBy(static f => f.Finalidade))
        {
            if (EstruturaFormulario.ValidarItens(formulario.Estrutura, RegrasDosFormularios.ItensDaFinalidade(configuracao, formulario.Finalidade)) is [{ } primeiro, ..])
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
        configuracao.Coleta.Formularios.OrderBy(static f => f.Finalidade)
            .Select(f => FormaDoItem.ValidarQuantidade(
                CitacoesDosFormularios.QuantidadeNoTeto(configuracao.Coleta.Itens.Count(i => i.Finalidade == f.Finalidade), CitacoesDosFormularios.GruposDaFinalidade(configuracao, f.Finalidade))))
            .FirstOrDefault(static erros => erros.Count > 0) is [var excesso, ..]
            ? excesso.Error
            : null;

    /// <summary>
    /// Termo exigido cuja versão ainda não definiu a forma de aceite não é publicável
    /// (UNI-REQ-0086): só o registro digital, com ou sem log de IP, é forma resolvida.
    /// </summary>
    internal static DomainError? PendenciaDeTermoSemFormaDeAceite(ConfiguracaoDeclarada configuracao) =>
        configuracao.Coleta.TermosExigidos.OrderBy(static t => t.Ordem).FirstOrDefault(static t => t.SemFormaDeAceite) is { } termo
            ? new DomainError(
                TermoExigidoFormularioErrorCodes.SemFormaDeAceite,
                $"O termo '{termo.Codigo}' usa uma versão sem forma de aceite definida; escolha uma versão com registro digital.")
            : null;
}
