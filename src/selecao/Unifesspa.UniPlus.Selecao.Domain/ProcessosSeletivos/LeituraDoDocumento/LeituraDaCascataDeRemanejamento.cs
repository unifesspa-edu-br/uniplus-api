namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A cascata de remanejamento do documento, lida pelas fábricas da entidade, na ordem da forma, da regra do catálogo, das modalidades do
/// cadastro e da conformidade com a regra. A ordem de cada destino é a posição dele entre os da mesma origem, a partir de 1, e a causa
/// de um destino aponta a posição enviada dele. A cascata é um bloco só: qualquer recusa a deixa fora, as recusas independentes se
/// acumulam, e só a conferência com a regra espera os dados resolvidos.
/// </summary>
internal static class LeituraDaCascataDeRemanejamento
{
    /// <summary>O nome do bloco, que é o caminho da parte recusada.</summary>
    internal const string Caminho = "ordemDeRemanejamento";

    /// <summary>O nome da lista de destinos na origem do documento.</summary>
    internal const string Lista = "ordemDeRemanejamento.ordens";

    internal static CascataLida Ler(CascataNoDocumento? cascata, CadastrosCitadosNoDocumento cadastros)
    {
        ArgumentNullException.ThrowIfNull(cadastros);

        return cascata is null ? CascataLida.Ausente : ComOsCadastros(cascata, cadastros);
    }

    private static CascataLida ComOsCadastros(CascataNoDocumento cascata, CadastrosCitadosNoDocumento cadastros)
    {
        DestinosLidos destinos = LerDestinos(cascata);
        IReadOnlyList<ParteRecusada> deForma = [.. Forma(cascata, destinos), .. destinos.Recusadas];
        RegraDoCatalogoCitada? regra = cadastros.Regra(cascata.Regra?.Codigo, cascata.Regra?.Versao);
        IReadOnlyList<ParteRecusada> recusadas =
            [.. deForma, .. RecusaDaRegra(regra), .. ModalidadesSemCadastro(cascata, cadastros, deForma)];

        return recusadas.Count > 0
            ? Recusada(recusadas)
            : Conferida(cascata, destinos, regra!);
    }

    private static IReadOnlyList<ParteRecusada> RecusaDaRegra(RegraDoCatalogoCitada? regra) => regra switch
    {
        null => DoBloco("regra.codigo", RecusasDeCadastroDasVagas.RegraDeRemanejamentoNaoEncontrada()),
        { Tipo: not TipoRegra.CriterioRemanejamento } => DoBloco("regra.codigo", RecusasDeCadastroDasVagas.RegraDeRemanejamentoDeTipoInvalido()),
        _ => [],
    };

    // O código que já tem recusa no campo dele (formato inválido, ausente ou igual à origem) não ganha uma segunda causa.
    private static IEnumerable<ParteRecusada> ModalidadesSemCadastro(
        CascataNoDocumento cascata, CadastrosCitadosNoDocumento cadastros, IReadOnlyList<ParteRecusada> jaRecusadas)
    {
        HashSet<string?> recusados = [.. jaRecusadas.Select(static parte => parte.Causa.Field)];

        return CodigosCitados(cascata)
            .Where(citado => cadastros.Modalidade(citado.Codigo) is null)
            .SelectMany(static citado => RecusasDasVagas.Partes(
                Caminho, citado.Posicao, citado.Item, [new FieldError(citado.Campo, RecusasDeCadastroDasVagas.ModalidadeNaoEncontrada())]))
            .Where(parte => !recusados.Contains(parte.Causa.Field));
    }

    private static IEnumerable<(int Posicao, string Item, string Campo, string? Codigo)> CodigosCitados(CascataNoDocumento cascata)
    {
        yield return (0, Caminho, "fallbackCodigo", cascata.DestinoFinalCodigo);

        foreach (DestinoDaCascataNoDocumento destino in cascata.Destinos)
        {
            string item = $"{Lista}[{destino.PosicaoNoDocumento}]";
            yield return (destino.PosicaoNoDocumento, item, "modalidadeOrigemCodigo", destino.ModalidadeOrigemCodigo);
            yield return (destino.PosicaoNoDocumento, item, "modalidadeDestinoCodigo", destino.ModalidadeDestinoCodigo);
        }
    }

    private static CascataLida Conferida(CascataNoDocumento cascata, DestinosLidos destinos, RegraDoCatalogoCitada regra)
    {
        Result<ConfiguracaoCascataRemanejamento> criada = ConfiguracaoCascataRemanejamento.Criar(
            regra.Referencia, cascata.DestinoFinalCodigo ?? string.Empty, destinos.Itens);
        if (criada.IsFailure)
        {
            return Recusada([.. RecusasDasVagas.Partes(Caminho, 0, Caminho, criada.Errors)]);
        }

        return ConformidadeDaCascataComARegra.Conferir(criada.Value!, regra.DeclaracaoDaRegra)
            ? new(criada.Value, destinos.Posicoes, [])
            : Recusada(DoBloco(string.Empty, RecusasDeCadastroDasVagas.MatrizDivergenteDaRegra()));
    }

    private static IReadOnlyList<ParteRecusada> Forma(CascataNoDocumento cascata, DestinosLidos destinos)
    {
        IEnumerable<FieldError> causas = destinos.Recusadas.Count > 0
            ? ConfiguracaoCascataRemanejamento.ValidarFallbackELimitesIndependentesDeItens(
                cascata.DestinoFinalCodigo, cascata.Destinos.Count, cascata.Destinos.Select(static destino => destino.ModalidadeOrigemCodigo))
            : ConfiguracaoCascataRemanejamento.ValidarForma(cascata.DestinoFinalCodigo ?? string.Empty, destinos.Itens);

        return RecusasDasVagas.Partes(Caminho, 0, Caminho, causas);
    }

    private static DestinosLidos LerDestinos(CascataNoDocumento cascata)
    {
        (DestinoDaCascataNoDocumento Documento, Result<DestinoRemanejamento> Criado)[] lidos =
        [
            .. cascata.DestinosComOrdem().Select(
                static item => (item.Destino, DestinoRemanejamento.Criar(item.Destino.ModalidadeOrigemCodigo, item.Ordem, item.Destino.ModalidadeDestinoCodigo))),
        ];

        return new(
            [.. lidos.Where(static lido => lido.Criado.IsSuccess).Select(static lido => lido.Criado.Value!)],
            [.. lidos.Where(static lido => lido.Criado.IsSuccess).Select(static lido => lido.Documento.PosicaoNoDocumento)],
            [.. lidos.SelectMany(static lido => RecusasDasVagas.Partes(
                Caminho, lido.Documento.PosicaoNoDocumento, $"{Lista}[{lido.Documento.PosicaoNoDocumento}]", lido.Criado.Errors))]);
    }

    private static IReadOnlyList<ParteRecusada> DoBloco(string campo, DomainError erro) =>
        RecusasDasVagas.Partes(Caminho, 0, Caminho, [new FieldError(campo.Length == 0 ? null : campo, erro)]);

    private static CascataLida Recusada(IReadOnlyList<ParteRecusada> partes) => new(null, [], partes);

    private sealed record DestinosLidos(
        IReadOnlyList<DestinoRemanejamento> Itens, IReadOnlyList<int> Posicoes, IReadOnlyList<ParteRecusada> Recusadas);
}
