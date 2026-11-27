namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// As bancas que a etapa requer, cada uma com a cópia do tipo que o cadastro tem agora. A banca cujo tipo ou regra de deliberação o
/// domínio recusa fica de fora inteira e vira parte recusada, que aponta o campo da banca que a causa; as causas independentes da
/// mesma banca acumulam. A banca que não cabe na lista suspende o que se conclui das bancas da etapa. Não confere se a consequência
/// da ausência cabe no tipo nem o mínimo de integrantes, que são da declaração da etapa. A etapa não tem candidatos atendidos por
/// categoria, e o que o documento trouxesse neles não é lido.
/// </summary>
internal static class LeituraDasBancasDaEtapa
{
    internal static ValorLido<IReadOnlyList<(BancaDaEtapa Item, int PosicaoNoDocumento)>> Ler(EtapaNoDocumento etapa, CadastrosCitadosNoDocumento cadastros)
    {
        ArgumentNullException.ThrowIfNull(etapa);
        ArgumentNullException.ThrowIfNull(cadastros);

        (BancaNoDocumento Citada, ValorLido<BancaDaEtapa?> Lida)[] lidas = [.. etapa.Bancas.Select(banca => (banca, Banca(banca, cadastros)))];

        return new(
            [.. lidas.Where(static par => par.Lida.Valor is not null).Select(static par => (par.Lida.Valor!, par.Citada.PosicaoNoDocumento))],
            [.. lidas.SelectMany(static par => par.Lida.Recusas)]);
    }

    private static ValorLido<BancaDaEtapa?> Banca(BancaNoDocumento banca, CadastrosCitadosNoDocumento cadastros)
    {
        TipoDeBancaCopiado? tipo = cadastros.TipoDeBanca(banca.TipoBancaCodigo);
        (RegraDeDeliberacao? regra, IReadOnlyList<FieldError> violacoes) = Deliberacao(banca.RegraDeDeliberacao);
        string campo = $"{EtapaDeclarada.ListaDasBancas}[{banca.PosicaoNoDocumento}]";
        Result<ConsequenciaDoDescumprimento?> consequencia = ConsequenciaDaAusenciaNaBanca.Ler(banca.ConsequenciaDaAusencia, $"{campo}.consequenciaDaAusencia");
        RecusaDaParte[] recusas = [.. DoTipo(banca, tipo, campo), .. DaDeliberacao(banca, violacoes, campo), .. consequencia.Errors.Select(erro => Recusa(banca, erro.Field!, erro.Error))];

        return recusas.Length > 0 || tipo is null
            ? new(null, recusas)
            : new(BancaDaEtapa.Criar(ChaveLocal.Criar(banca.Chave).Value, tipo, regra, null, consequencia.Value), []);
    }

    private static (RegraDeDeliberacao? Regra, IReadOnlyList<FieldError> Violacoes) Deliberacao(DeclaracaoDaDeliberacao? declarada)
    {
        if (declarada is null)
        {
            return (null, []);
        }

        Result<RegraDeDeliberacao> regra = RegraDeDeliberacao.Criar(declarada);

        return (regra.Value, regra.IsFailure ? regra.Errors : []);
    }

    private static IEnumerable<RecusaDaParte> DoTipo(BancaNoDocumento banca, TipoDeBancaCopiado? tipo, string campo) =>
        tipo is null
            ? [Recusa(banca, $"{campo}.tipoBancaId", new DomainError(
                EtapaProcessoErrorCodes.TipoBancaNaoEncontradoOuInativo,
                "O tipo de banca indicado não existe ou está desativado no cadastro."))]
            : [];

    private static IEnumerable<RecusaDaParte> DaDeliberacao(BancaNoDocumento banca, IReadOnlyList<FieldError> violacoes, string campo) =>
        violacoes.Select(violacao => Recusa(banca, violacao.NoCaminhoDoItem($"{campo}.regraDeDeliberacao").Field!, violacao.Error));

    private static RecusaDaParte Recusa(BancaNoDocumento banca, string campo, DomainError erro) =>
        new(EtapaDeclarada.ListaDasBancas, campo, banca.PosicaoNoDocumento, banca.Chave, erro);
}
