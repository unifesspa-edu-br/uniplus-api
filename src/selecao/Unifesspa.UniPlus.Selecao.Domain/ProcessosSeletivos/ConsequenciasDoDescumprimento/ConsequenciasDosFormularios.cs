namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConsequenciasDoDescumprimento;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.OrdemCanonica;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// As consequências da falta de envio declaradas nos formulários. O pedido de isenção é facultativo, e quem não se
/// inscreve não chega a ter o que perder: nesses formulários a declaração não cabe, e é a única causa deles, sem as
/// causas das demais famílias. Nos outros, a declaração é conferida como a das exigências: o acréscimo só se o
/// processo o oferece, e a modalidade só se há modalidade em questão, entendida como a que as exigências de documento
/// do formulário alcançam. A posição de cada formulário no caminho é a canônica.
/// </summary>
internal static class ConsequenciasDosFormularios
{
    private const string ListaDosFormularios = "formularios";

    internal static IEnumerable<FieldError> Facultativos(ConfiguracaoDeclarada configuracao) =>
        Declarados(configuracao).Where(par => Facultativo(par.Formulario)).Select(par => Recusa(
            par, FormularioProcessoErrorCodes.ConsequenciaEmFormularioFacultativo, "é facultativo, e a falta de envio dele não pode ter consequência declarada"));

    internal static IEnumerable<FieldError> DaInscricao(ConfiguracaoDeclarada configuracao, CronogramaDoProcesso cronograma) =>
        Declarados(configuracao).Where(par => !Facultativo(par.Formulario) && ColetaInscricao(cronograma, par.Formulario)).Select(par => Recusa(
            par, FormularioProcessoErrorCodes.ConsequenciaEmFormularioDaInscricao, "coleta a inscrição, e a falta de envio dele não pode ter consequência declarada"));

    internal static IEnumerable<FieldError> Acrescimo(ConfiguracaoDeclarada configuracao, CronogramaDoProcesso cronograma) =>
        Avaliaveis(configuracao, cronograma).Where(par => CoerenciaDaConsequenciaDoDescumprimento.AcrescimoNaoOferecido(configuracao, par.Formulario.ConsequenciaDaFaltaDeEnvio))
            .Select(par => CoerenciaDaConsequenciaDoDescumprimento.Acrescimo(par.Caminho, $"O formulário '{par.Formulario.Finalidade.Codigo()}'"));

    internal static IEnumerable<FieldError> SemModalidade(ConfiguracaoDeclarada configuracao, FatosDeModalidade fatos, CronogramaDoProcesso cronograma) =>
        ComRetirada(configuracao, cronograma).Where(par => CoerenciaDaConsequenciaDoDescumprimento.SemModalidade(configuracao, modalidade => Alcanca(configuracao, par.Formulario, modalidade, fatos)))
            .Select(par => CoerenciaDaConsequenciaDoDescumprimento.SemModalidade(par.Caminho));

    internal static IEnumerable<FieldError> SemAmpla(ConfiguracaoDeclarada configuracao, FatosDeModalidade fatos, CronogramaDoProcesso cronograma) =>
        ComRetirada(configuracao, cronograma).Where(par => CoerenciaDaConsequenciaDoDescumprimento.SemAmpla(configuracao, modalidade => Alcanca(configuracao, par.Formulario, modalidade, fatos)))
            .Select(par => CoerenciaDaConsequenciaDoDescumprimento.SemAmpla(par.Caminho));

    private static IEnumerable<(FormularioProcesso Formulario, string Caminho)> Declarados(ConfiguracaoDeclarada configuracao)
    {
        FormularioProcesso[] formularios = [.. OrdemCanonicaDaConfiguracao.Formularios(configuracao.Coleta.Formularios)];
        return Enumerable.Range(0, formularios.Length).OrderBy(indice => configuracao.Origem.PosicaoDeOrigem(ListaDosFormularios, indice))
            .Select(indice => (Formulario: formularios[indice], Caminho: $"{ListaDosFormularios}[{indice}].{LeituraDaConsequenciaDaFaltaDeEnvio.Campo}"))
            .Where(par => par.Formulario.ConsequenciaDaFaltaDeEnvio is not null && !CoerenciaDaConsequenciaDoDescumprimento.Recusada(configuracao, par.Caminho));
    }

    /// <summary>Se algum formulário que declara consequência tem a fase sem julgar: o painel não aprova o que a conferência não pôde avaliar.</summary>
    internal static bool ComFaseSemJulgar(ConfiguracaoDeclarada configuracao, CronogramaDoProcesso cronograma) =>
        Declarados(configuracao).Any(par => FaseSemJulgar(cronograma, par.Formulario));

    private static IEnumerable<(FormularioProcesso Formulario, string Caminho)> Avaliaveis(ConfiguracaoDeclarada configuracao, CronogramaDoProcesso cronograma) =>
        Declarados(configuracao).Where(par => !Facultativo(par.Formulario) && !ColetaInscricao(cronograma, par.Formulario) && !FaseSemJulgar(cronograma, par.Formulario));

    // O formulário de inscrição e o facultativo são julgados sem a fase: um não depende dela, e o outro não declara.
    private static bool FaseSemJulgar(CronogramaDoProcesso cronograma, FormularioProcesso formulario) =>
        !Facultativo(formulario) && formulario.Finalidade != FinalidadeFormulario.Inscricao && cronograma.FaseSemJulgar(formulario.FaseId);

    private static IEnumerable<(FormularioProcesso Formulario, string Caminho)> ComRetirada(ConfiguracaoDeclarada configuracao, CronogramaDoProcesso cronograma) =>
        Avaliaveis(configuracao, cronograma).Where(par => par.Formulario.ConsequenciaDaFaltaDeEnvio == ConsequenciaDoDescumprimento.RetiraAModalidade
            && !CoerenciaDaConsequenciaDoDescumprimento.Recusada(configuracao, "distribuicaoVagas")
            && !CoerenciaDaConsequenciaDoDescumprimento.Recusada(configuracao, "documentosExigidos"));

    private static bool Alcanca(ConfiguracaoDeclarada configuracao, FormularioProcesso formulario, string modalidade, FatosDeModalidade fatos) =>
        configuracao.Exigencias.DocumentosExigidos.Any(exigencia => exigencia.Finalidade == formulario.Finalidade && exigencia.PodeAlcancarModalidade(modalidade, fatos));

    // O pedido de isenção da taxa é o formulário facultativo: o candidato o envia se quiser.
    private static bool Facultativo(FormularioProcesso formulario) => formulario.Finalidade == FinalidadeFormulario.IsencaoTaxa;

    private static bool ColetaInscricao(CronogramaDoProcesso cronograma, FormularioProcesso formulario) =>
        formulario.Finalidade == FinalidadeFormulario.Inscricao || cronograma.FaseColetaInscricao(formulario.FaseId);

    private static FieldError Recusa((FormularioProcesso Formulario, string Caminho) par, string codigo, string motivo) =>
        new(par.Caminho, new DomainError(codigo, $"O formulário '{par.Formulario.Finalidade.Codigo()}' {motivo}."));
}
