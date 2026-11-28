namespace Unifesspa.UniPlus.Configuracao.Application.Commands.ModelosFormulario;

using Unifesspa.UniPlus.Configuracao.Domain.Services;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// A leitura de um campo do modelo, item ou campo de grupo: o tipo de campo, as regras e o formato do fato no catálogo.
/// O campo que a leitura recusa não é lido, mas passa pelos textos que o banco não grava e pela forma do item que não
/// dependem do que foi recusado; as recusas dele saem na ordem da leitura, dos textos e da forma.
/// </summary>
/// <param name="formatos">O formato do campo de texto de cada fato, pelo código na forma gravada.</param>
internal sealed class LeituraDosCamposDoModelo(RecusasDaLeituraDoModelo recusas, IReadOnlyDictionary<string, string> formatos)
{
    /// <summary>O dado que cada campo recusado coleta, como veio no pedido: não depende do que a leitura recusou nele.</summary>
    public List<CampoNaoLido> CamposNaoLidos { get; } = [];

    public ItemDoModelo? LerItem(FatoColetadoInput entrada, string campo, LugarDoCampo lugar)
    {
        int recusasAntes = recusas.Recusas.Count;
        TipoDeCampo? tipo = LerTipoDeCampo(entrada.Apresentacao?.TipoDeCampo, $"{campo}.{FatoColetadoInput.CaminhoDaApresentacao}");
        RegrasLidasDoCampo regras = LerRegrasDoCampo(entrada.Regras, $"{campo}.{FatoColetadoInput.CaminhoDasRegras}");
        return recusas.RecusouDesde(recusasAntes)
            ? RecusarItemNaoLido(entrada, (campo, lugar), tipo, regras)
            : new ItemDoModelo(
                entrada.FatoCodigo,
                entrada.Ordem,
                entrada.PaginaCodigo,
                EntradaDeRegras.Apresentacao(entrada.Apresentacao, tipo!.Value),
                FormatoDoCatalogo(entrada.FatoCodigo),
                new RegrasDoCampo(regras.Exibicao, regras.Obrigatoriedade!, [.. regras.Restricoes.OfType<RestricaoValor>()], regras.Impedimento));
    }

    /// <summary>
    /// Lê os campos do grupo e guarda, na ordem da entrada, as recusas de cada um: as da leitura, dos textos e da forma, se não se
    /// leu, ou as dos textos e da forma, se se leu.
    /// </summary>
    public SubitensLidos LerSubitens(GrupoColetadoInput entrada, string campo, int posicaoDoGrupo)
    {
        List<(int Indice, ItemDoModelo Parte)> lidos = [];
        List<FieldError> recusasNaOrdem = [];
        foreach ((int indice, FatoColetadoInput? subitem) in (entrada.Subitens ?? []).Index())
        {
            (ItemDoModelo? item, List<FieldError> doSubitem) = LerSubitem(subitem, $"{campo}.subitens[{indice}]", new LugarDoCampo(posicaoDoGrupo, indice));
            if (item is not null)
            {
                lidos.Add((indice, item));
            }

            recusasNaOrdem.AddRange(doSubitem);
        }

        return new SubitensLidos(lidos, recusasNaOrdem);
    }

    private (ItemDoModelo? Item, List<FieldError> Recusas) LerSubitem(FatoColetadoInput? subitem, string caminho, LugarDoCampo lugar)
    {
        int antes = recusas.Recusas.Count;
        ItemDoModelo? item = recusas.LerPresente(subitem, caminho, (presente, campo) => LerItem(presente, campo, lugar));
        return (item, item is null ? recusas.Recusas.GetRange(antes, recusas.Recusas.Count - antes) : [.. RecusasDoSubitemLido(item, caminho)]);
    }

    private static IEnumerable<FieldError> RecusasDoSubitemLido(ItemDoModelo item, string caminho)
    {
        RegrasLidasDoCampo regras = RegrasLidasDoCampo.De(item.Regras);
        return RecusasDaLeituraDoModelo.TextosNaoGravaveis(TextosDoConteudoDoModelo.DoCampo(
                caminho, item.FatoCodigo, item.PaginaCodigo, item.Apresentacao.Rotulo, item.Apresentacao.Ajuda, item.Formato, regras))
            .Concat(FormaDoItem.Conferir(item.FatoCodigo, item.Ordem, ApresentacaoLidaDoCampo.De(item.Apresentacao), item.Formato, regras).Select(erro => erro.NoCaminhoDoItem(caminho)));
    }

    /// <summary>Recusa os textos e a forma do item que a leitura recusou, para as recusas dele saírem juntas; devolve nulo, o item não lido.</summary>
    private ItemDoModelo? RecusarItemNaoLido(
        FatoColetadoInput entrada, (string Caminho, LugarDoCampo Lugar) onde, TipoDeCampo? tipo, RegrasLidasDoCampo regras)
    {
        string campo = onde.Caminho;
        string? formato = FormatoDoCatalogo(entrada.FatoCodigo);
        if (CodigoFatoCandidato.EhValido(entrada.FatoCodigo))
        {
            CamposNaoLidos.Add(new CampoNaoLido(campo, entrada.FatoCodigo, onde.Lugar));
        }

        recusas.Recusas.AddRange(RecusasDaLeituraDoModelo.TextosNaoGravaveis(TextosDoConteudoDoModelo.DoCampo(
            campo, entrada.FatoCodigo, entrada.PaginaCodigo, entrada.Apresentacao?.Rotulo, entrada.Apresentacao?.Ajuda, formato, regras)));
        recusas.Recusas.AddRange(FormaDoItem.Conferir(entrada.FatoCodigo, entrada.Ordem, ApresentacaoLidaDoCampo.DaEntrada(entrada.Apresentacao, tipo), formato, regras).Select(erro => erro.NoCaminhoDoItem(campo)));
        return null;
    }

    private RegrasLidasDoCampo LerRegrasDoCampo(RegrasDoCampoInput? entrada, string campo)
    {
        PredicadoDnf? exibicao = recusas.LerPredicado(entrada?.Exibicao, $"{campo}.exibicao");
        Obrigatoriedade? obrigatoriedade = recusas.LerObrigatoriedade(
            entrada?.Obrigatoriedade?.Tipo, entrada?.Obrigatoriedade?.Predicado, $"{campo}.obrigatoriedade", $"{campo}.obrigatoriedade.predicado");
        List<RestricaoValor?> restricoes = LerRestricoes(entrada?.Restricoes, campo);
        return new RegrasLidasDoCampo(exibicao, obrigatoriedade, restricoes, LerImpedimento(entrada?.Impedimento, campo));
    }

    private TipoDeCampo? LerTipoDeCampo(string? codigo, string campo)
    {
        Result<TipoDeCampo> tipo = TipoDeCampoDaEntrada.Ler(codigo);
        if (tipo.IsFailure)
        {
            recusas.Recusas.Add(new($"{campo}.tipoDeCampo", tipo.Error!));
        }

        return tipo.IsSuccess ? tipo.Value : null;
    }

    private List<RestricaoValor?> LerRestricoes(IReadOnlyList<RestricaoValorInput?>? entradas, string campo) =>
        [.. (entradas ?? []).Select((entrada, indice) => LerRestricao(entrada, $"{campo}.restricoes[{indice}]"))];

    private RestricaoValor? LerRestricao(RestricaoValorInput? entrada, string campo)
    {
        Result<RestricaoValor> restricao = EntradaDeRegras.Restricao(entrada);
        if (restricao.IsFailure)
        {
            recusas.Recusas.AddRange(restricao.Errors.Select(e => e.NoCaminhoDoItem(campo)));
        }

        return restricao.Value;
    }

    private Impedimento? LerImpedimento(ImpedimentoInput? entrada, string campo)
    {
        Result<Impedimento?> impedimento = EntradaDeRegras.Impedimento(entrada);
        if (impedimento.IsFailure)
        {
            recusas.Recusas.AddRange(impedimento.Errors.Select(e => e.NoCaminhoDoItem($"{campo}.impedimento.quando")));
        }

        return impedimento.Value;
    }

    private string? FormatoDoCatalogo(string? fatoCodigo) =>
        fatoCodigo is { } codigo && formatos.TryGetValue(codigo, out string? formato) ? formato : null;
}

/// <summary>Os campos de um grupo que a leitura aceitou, com a posição de cada um, e as recusas de todos, na ordem da entrada.</summary>
internal sealed record SubitensLidos(List<(int Indice, ItemDoModelo Parte)> Lidos, List<FieldError> RecusasNaOrdem);
