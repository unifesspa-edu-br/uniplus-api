namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Errors;
using Domain.ProcessosSeletivos.Bancas;
using Domain.ProcessosSeletivos.Cronograma;
using Domain.ValueObjects;

using Kernel.Domain.CodigosDeContrato;
using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>
/// Monta as bancas requeridas de uma fase. A banca nova ou que troca de tipo copia o tipo de banca do cadastro vivo; a banca
/// que continua com o mesmo tipo mantém a cópia que já tem, sem ler o cadastro, e por isso o tipo que saiu do cadastro não
/// a afeta. Os candidatos atendidos são resolvidos no cadastro vivo e congelados por valor: uma categoria que saiu do cadastro
/// não pode entrar no edital, porque os candidatos atendidos congelados respondem por parecer e recurso do certame inteiro. As
/// violações da banca de uma mesma fase acumulam: o que falta numa não esconde o que falta nas outras.
/// </summary>
internal sealed class MontagemDasBancasDaFase(CadastrosDasFases cadastros, ChavesDasBancasDoProcesso chaves)
{
    public async Task<Result<List<BancaRequerida>>> MontarAsync(
        FaseCronogramaInput fase, int indice, IReadOnlyCollection<BancaRequerida> gravadas, CancellationToken cancellationToken)
    {
        RecusasAcumuladas<BancaRequerida> bancas = new();
        for (int posicao = 0; posicao < fase.BancasRequeridas.Count; posicao++)
        {
            string campo = $"fases[{indice}].{FaseDeclarada.ListaDasBancasRequeridas}[{posicao}]";
            bancas.Juntar(await BancaAsync(fase.BancasRequeridas[posicao], gravadas, campo, cancellationToken).ConfigureAwait(false));
        }

        return bancas.Resultado();
    }

    private async Task<Result<BancaRequerida>> BancaAsync(
        BancaRequeridaInput declarada, IReadOnlyCollection<BancaRequerida> gravadas, string campo, CancellationToken cancellationToken)
    {
        TipoDeBancaCopiado? tipo = await TipoAsync(declarada, gravadas, cancellationToken).ConfigureAwait(false);
        Result<List<CategoriaJulgada>> atendidos = await CandidatosAtendidosAsync(declarada, campo, cancellationToken).ConfigureAwait(false);
        (Result<RegraDeDeliberacao?> regra, ConsequenciaDoDescumprimento? consequencia, FieldError[] independentes) = Independentes(declarada, tipo, campo);
        FieldError[] erros = [.. TipoNaoEncontrado(tipo, campo), .. atendidos.Errors, .. independentes];
        return tipo is null || erros.Length > 0
            ? Result<BancaRequerida>.ValidationFailure(erros)
            : Result<BancaRequerida>.Success(BancaRequerida.Criar(ChaveDa(declarada, tipo, gravadas), tipo, atendidos.Value!, regra.Value, declarada.Id, consequencia));
    }

    private static FieldError[] TipoNaoEncontrado(TipoDeBancaCopiado? tipo, string campo) => tipo is not null ? [] :
    [
        new($"{campo}.tipoBancaId", new DomainError(
            FaseCronogramaErrorCodes.TipoBancaNaoEncontrado,
            "O tipo de banca indicado não existe ou está desativado no cadastro.")),
    ];

    private ChaveLocal ChaveDa(BancaRequeridaInput declarada, TipoDeBancaCopiado tipo, IReadOnlyCollection<BancaRequerida> gravadas) =>
        gravadas.FirstOrDefault(banca => banca.Id == declarada.Id)?.Chave ?? chaves.Formar(tipo.Codigo.Codigo());

    private async Task<TipoDeBancaCopiado?> TipoAsync(
        BancaRequeridaInput declarada, IReadOnlyCollection<BancaRequerida> gravadas, CancellationToken cancellationToken)
    {
        if (CopiaMantidaDaBanca.De(gravadas, declarada.Id, declarada.TipoBancaId) is { } mantida)
        {
            return mantida;
        }

        TipoBancaView? tipo = await cadastros.TiposDeBanca.ObterPorIdAsync(declarada.TipoBancaId, cancellationToken).ConfigureAwait(false);

        return tipo is null ? null : CopiaDoTipoDeBanca.De(tipo);
    }

    private static (Result<RegraDeDeliberacao?> Regra, ConsequenciaDoDescumprimento? Consequencia, FieldError[] Erros) Independentes(BancaRequeridaInput declarada, TipoDeBancaCopiado? tipo, string campo)
    {
        Result<RegraDeDeliberacao?> regra = MontagemDaRegraDeDeliberacao.Montar(declarada.RegraDeDeliberacao, campo);
        string campoDaConsequencia = $"{campo}.consequenciaDaAusencia";
        Result<ConsequenciaDoDescumprimento?> consequencia = ConsequenciaDaAusenciaNaBanca.Ler(declarada.ConsequenciaDaAusencia, campoDaConsequencia);
        return (regra, consequencia.Value, [.. regra.Errors, .. consequencia.Errors, .. (tipo is null ? [] : ConsequenciaDaAusenciaNaBanca.Violacao(tipo, consequencia.Value, campoDaConsequencia))]);
    }

    private async Task<Result<List<CategoriaJulgada>>> CandidatosAtendidosAsync(
        BancaRequeridaInput declarada, string campo, CancellationToken cancellationToken)
    {
        List<CategoriaJulgada> atendidos = [];
        foreach ((int posicao, Guid categoriaId) in declarada.CategoriasDocumentoIds.Index())
        {
            CategoriaDocumentoView? categoria = await cadastros.Categorias.ObterAsync(categoriaId, cancellationToken).ConfigureAwait(false);
            if (categoria is null)
            {
                return CategoriaNaoEncontrada($"{campo}.categoriasDocumentoIds[{posicao}]");
            }

            atendidos.Add(CategoriaJulgada.Criar(categoria.Id, categoria.Codigo, categoria.Nome));
        }

        return Result<List<CategoriaJulgada>>.Success(atendidos);
    }

    private static Result<List<CategoriaJulgada>> CategoriaNaoEncontrada(string campoDaCategoria) =>
        InterrupcaoDaEdicao.Com<List<CategoriaJulgada>>(campoDaCategoria, new DomainError(
            "FaseCronograma.CategoriaDocumentoNaoEncontrada",
            "Uma categoria de documento dos candidatos atendidos pela banca não existe ou está desativada no cadastro."));
}
