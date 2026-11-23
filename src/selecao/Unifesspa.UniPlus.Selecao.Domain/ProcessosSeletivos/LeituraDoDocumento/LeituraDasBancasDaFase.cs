namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// As bancas que a fase requer, cada uma com a cópia do tipo que o cadastro tem agora. A banca cujo tipo (o código é obrigatório no documento, e aqui só falta o que o cadastro não tem), categoria dos candidatos atendidos ou regra de deliberação o domínio recusa fica de fora inteira
/// e vira parte recusada, que aponta o campo da banca que a causa; as causas independentes da mesma banca acumulam. A banca que
/// não cabe na lista suspende o que se conclui das bancas da fase. Não confere os candidatos atendidos nem o mínimo de integrantes.
/// </summary>
internal static class LeituraDasBancasDaFase
{
    internal static ValorLido<IReadOnlyList<(BancaRequerida Item, int PosicaoNoDocumento)>> Ler(FaseNoDocumento fase, CadastrosCitadosNoDocumento cadastros)
    {
        (BancaNoDocumento Citada, ValorLido<BancaRequerida?> Lida)[] lidas = [.. fase.Bancas.Select(banca => (banca, Banca(banca, cadastros)))];

        return new(
            [.. lidas.Where(static par => par.Lida.Valor is not null).Select(static par => (par.Lida.Valor!, par.Citada.PosicaoNoDocumento))],
            [.. lidas.SelectMany(static par => par.Lida.Recusas)]);
    }

    private static ValorLido<BancaRequerida?> Banca(BancaNoDocumento banca, CadastrosCitadosNoDocumento cadastros)
    {
        TipoDeBancaCopiado? tipo = cadastros.TipoDeBanca(banca.TipoBancaCodigo);
        CategoriaDeDocumentoCitada?[] categorias = [.. banca.CategoriasDocumentoCodigos.Select(cadastros.Categoria)];
        (RegraDeDeliberacao? regra, IReadOnlyList<FieldError> violacoes) = Deliberacao(banca.RegraDeDeliberacao);
        RecusaDaParte[] recusas = [.. DoTipo(banca, tipo), .. DasCategorias(banca, categorias), .. DaDeliberacao(banca, violacoes)];

        return recusas.Length > 0
            ? new(null, recusas)
            : new(BancaRequerida.Criar(ChaveLocal.Criar(banca.Chave).Value, tipo!, [.. categorias.Select(static categoria => CategoriaJulgada.Criar(categoria!.OrigemId, categoria.Codigo))], regra), []);
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

    private static IEnumerable<RecusaDaParte> DoTipo(BancaNoDocumento banca, TipoDeBancaCopiado? tipo) =>
        tipo is null
            ? [Recusa(banca, $"bancasRequeridas[{banca.PosicaoNoDocumento}].tipoBancaId", new DomainError(
                FaseCronogramaErrorCodes.TipoBancaNaoEncontrado,
                "O tipo de banca indicado não existe ou está desativado no cadastro."))]
            : [];

    private static IEnumerable<RecusaDaParte> DasCategorias(BancaNoDocumento banca, CategoriaDeDocumentoCitada?[] categorias) =>
        categorias
            .Select(static (categoria, posicao) => (categoria, posicao))
            .Where(static par => par.categoria is null)
            .Select(par => Recusa(banca, $"bancasRequeridas[{banca.PosicaoNoDocumento}].categoriasDocumentoIds[{par.posicao}]", new DomainError(
                FaseCronogramaErrorCodes.CategoriaDocumentoNaoEncontrada,
                "Uma categoria de documento dos candidatos atendidos pela banca não existe ou está desativada no cadastro.")));

    private static IEnumerable<RecusaDaParte> DaDeliberacao(BancaNoDocumento banca, IReadOnlyList<FieldError> violacoes) =>
        violacoes.Select(violacao => Recusa(banca, $"bancasRequeridas[{banca.PosicaoNoDocumento}].regraDeDeliberacao.{violacao.Field}", violacao.Error));

    private static RecusaDaParte Recusa(BancaNoDocumento banca, string campo, DomainError erro) =>
        new(FaseDeclarada.ListaDasBancasRequeridas, campo, banca.PosicaoNoDocumento, banca.Chave, erro);
}
