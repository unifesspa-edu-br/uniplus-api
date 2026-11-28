namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.ImportacaoDoResultado;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConferenciaDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ImportacaoDoResultado;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>Os processos de resultado importado e de inscrição pelo sistema, reais, e a leitura das recusas e do painel deles.</summary>
internal static class ProcessoDaImportacaoDeTeste
{
    internal static readonly RelatorioInformado[] UmRelatorio = [new("RELATORIO_DE_TESTE", "v1")];

    /// <summary>Resultado importado, com a importação declarada na única fase do cronograma e o formulário obrigatório nela.</summary>
    internal static ProcessoSeletivo ImportadoConforme() => ApoioConformidadePublicabilidadeEstrutural.ProcessoConforme();

    internal static ProcessoSeletivo InscricaoPeloSistema() => ProcessoConformeFactory.Criar();

    internal static Guid PrimeiraFase(ProcessoSeletivo processo) => processo.ImportacaoDoResultado!.PrimeiraFaseDepoisDaImportacaoId!.Value;

    /// <summary>Tira o formulário da primeira fase depois da importação e o põe de volta com a obrigatoriedade pedida, na fase pedida.</summary>
    internal static void RedefinirFormulario(ProcessoSeletivo processo, Guid faseId, bool obrigatorio)
    {
        processo.RemoverFormulario(PapelDoFormularioDeTeste.Habilitacao).IsSuccess.Should().BeTrue();
        FormularioDeclarado declarado = new(FormularioDeTeste.CodigoDoPapel(PapelDoFormularioDeTeste.Habilitacao), faseId, obrigatorio);
        processo.DefinirFormulario(declarado, null, FormularioDeTeste.Paginas(PapelDoFormularioDeTeste.Habilitacao)).IsSuccess.Should().BeTrue();
    }

    internal static IReadOnlyList<FieldError> CausasDaImportacao(ProcessoSeletivo processo) =>
        [.. CausasQueImpedemCongelar.Assuntos
            .Single(static assunto => assunto.Nome == NomeDoAssunto.ImportacaoDoResultado)
            .Causas(ContextoDaConferencia.De(processo.Declaracao(), CadastrosDeTeste.Para(processo)))];

    internal static IReadOnlyList<string> CodigosDaRecusaDoFormulario(ProcessoSeletivo processo) =>
        [.. ConfiguracaoAptaACongelar.Conferir(processo.Id, processo.Declaracao(), CadastrosDeTeste.Para(processo)).Errors
            .Select(static causa => causa.Error.Code)
            .Where(static codigo => codigo == FormularioProcessoErrorCodes.PosImportacaoSemFormularioObrigatorio)];

    internal static IReadOnlyList<string> CodigosDaImportacao(ProcessoSeletivo processo) =>
        [.. CausasDaImportacao(processo).Select(static causa => causa.Error.Code)];

    internal static ItemConformidade Item(ProcessoSeletivo processo, string codigo) =>
        ChecklistDeConformidade.AvaliarConformidade(processo.Declaracao(), CadastrosDeTeste.Para(processo)).Single(item => item.Codigo == codigo);
}
