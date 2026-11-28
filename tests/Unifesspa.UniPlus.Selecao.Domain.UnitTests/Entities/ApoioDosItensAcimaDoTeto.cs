namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>A declaração com mais itens que o teto do formulário, que a gravação recusa e a conferência da publicação cobre.</summary>
internal static class ApoioDosItensAcimaDoTeto
{
    /// <summary>
    /// A gravação recusa acima do teto, então a declaração com itens a mais é montada à parte: a conferência cobre o
    /// rascunho que chega por outro caminho.
    /// </summary>
    internal static ConfiguracaoDeclarada ComItensAcimaDoTeto(ProcessoSeletivo processo)
    {
        ConfiguracaoDeclarada raiz = processo.Declaracao();
        FatoColetado[] excedentes = ItensVinculadosAoFormulario(processo.FormularioDe(PapelDoFormularioDeTeste.Inscricao)!, FormaDoItem.MaximoDeItens + 1);

        return new ConfiguracaoDeclarada(
            raiz.Identidade, raiz.Cronograma, raiz.Avaliacao, raiz.Vagas, raiz.Exigencias, ColetaComItens(raiz.Coleta, excedentes), raiz.DivulgacaoETaxa)
        {
            ProvaPresencial = raiz.ProvaPresencial,
            ImportacaoDoResultado = raiz.ImportacaoDoResultado,
            MotivosDeDecisao = raiz.MotivosDeDecisao,
            Origem = raiz.Origem,
        };
    }

    private static ColetaDeclarada ColetaComItens(ColetaDeclarada coleta, FatoColetado[] itens) => new(
        [.. coleta.Campos, .. itens], coleta.GruposColetados, coleta.Formularios, coleta.TermosExigidos,
        coleta.OpcoesDeclaradas, coleta.RegrasDerivacao, coleta.ReferenciaTemporalFatos);

    private static FatoColetado[] ItensVinculadosAoFormulario(FormularioProcesso formulario, int quantidade)
    {
        FatoColetado[] itens = [.. Enumerable.Range(0, quantidade).Select(static i => FatoColetadoDeTeste.Coletando($"FATO_{i}", FormularioDeTeste.PrimeiraOrdemDeInscricao + i)
            .ComRotulo("Campo")
            .ComObrigatoriedade(Obrigatoriedade.Sempre)
            .NaSecao(FormularioDeTeste.Secao)
            .Construir())];
        foreach (FatoColetado item in itens)
        {
            item.VincularFormulario(formulario);
        }

        return itens;
    }
}
