namespace Unifesspa.UniPlus.Selecao.Application.Services;

using System.Text.Json;

using Unifesspa.UniPlus.Authorization.Projecao;
using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.DTOs.Administrativos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O resolvedor da inscrição: para cada resposta, a proteção que o processo congelou (classificação e base legal), o
/// rótulo, a descrição, o tipo e o componente, e o valor na forma do tipo (ADR-0153). É o único lugar que responde "qual é
/// a proteção deste item" para a consulta administrativa; a permissão que decide a saída é do projetor.
/// </summary>
/// <remarks>
/// O identificador mantém a classificação própria e carrega só o valor mascarado: a fonte sem a transformação nunca sai.
/// O valor sai pelo par do identificador. A resposta ausente ou vazia
/// não gera item. A classificação que não se resolve vale como sensível.
/// </remarks>
internal static class ItensDaInscricao
{
    /// <summary>A máscara total dos identificadores que não são o CPF, até a proteção de dados definir a própria.</summary>
    public const string MascaraTotal = "•••••••";

    /// <summary>As respostas do formulário agrupadas por seção, na ordem de coleta, com todos os itens, sem decidir a saída.</summary>
    public static IReadOnlyList<SecaoDaInscricaoDto> Secoes(
        IReadOnlyDictionary<string, JsonElement> respostas,
        IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> gruposRespondidos,
        FatosDaVersao fatos,
        bool podeRevelar)
    {
        ArgumentNullException.ThrowIfNull(respostas);
        ArgumentNullException.ThrowIfNull(gruposRespondidos);
        ArgumentNullException.ThrowIfNull(fatos);

        return AgruparPorSecao(
            ItensDeFora(respostas, fatos, podeRevelar), GruposRespondidos(gruposRespondidos, fatos, podeRevelar), fatos.TitulosDasSecoes);
    }

    private sealed record ItemNaSecao(int Ordem, string Secao, ItemDeRespostaDto Item);

    private sealed record GrupoNaSecao(int Ordem, string Secao, GrupoDaInscricaoDto Grupo);

    private static List<ItemNaSecao> ItensDeFora(IReadOnlyDictionary<string, JsonElement> respostas, FatosDaVersao fatos, bool podeRevelar)
    {
        List<ItemNaSecao> itens = [];
        foreach ((string codigo, JsonElement valor) in respostas)
        {
            if (fatos.Coletados.TryGetValue(codigo, out FatoDoProcesso? fato) && Item(fato, valor, podeRevelar) is { } item)
            {
                itens.Add(new ItemNaSecao(fato.Ordem, fato.Secao ?? string.Empty, item));
            }
        }

        return itens;
    }

    private static List<GrupoNaSecao> GruposRespondidos(
        IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> respondidos, FatosDaVersao fatos, bool podeRevelar) =>
    [
        .. fatos.Grupos
            .Where(g => respondidos.ContainsKey(g.Codigo))
            .Select(g => new GrupoNaSecao(
                g.Ordem,
                g.Secao ?? string.Empty,
                new GrupoDaInscricaoDto(g.Codigo, g.Rotulo, [.. respondidos[g.Codigo].Select(o => Ocorrencia(g, o, podeRevelar))]))),
    ];

    private static OcorrenciaDaInscricaoDto Ocorrencia(GrupoDoProcesso grupo, OcorrenciaDeGrupoInscricao ocorrencia, bool podeRevelar) =>
        new(
            ocorrencia.Id,
            [.. ocorrencia.Respostas
                .Where(r => grupo.Subitens.ContainsKey(r.Key))
                .OrderBy(r => grupo.Subitens[r.Key].Ordem)
                .Select(r => Item(grupo.Subitens[r.Key], r.Value, podeRevelar))
                .OfType<ItemDeRespostaDto>()]);

    private static List<SecaoDaInscricaoDto> AgruparPorSecao(
        List<ItemNaSecao> itens, List<GrupoNaSecao> grupos, IReadOnlyDictionary<string, string> titulos) =>
    [
        .. itens.Select(static i => i.Secao).Concat(grupos.Select(static g => g.Secao)).Distinct(StringComparer.Ordinal)
            .Select(secao => (Ordem: PrimeiraOrdem(secao, itens, grupos), Dto: Secao(secao, itens, grupos, titulos)))
            .OrderBy(static s => s.Ordem)
            .Select(static s => s.Dto),
    ];

    private static int PrimeiraOrdem(string secao, List<ItemNaSecao> itens, List<GrupoNaSecao> grupos) =>
        itens.Where(i => i.Secao == secao).Select(static i => i.Ordem)
            .Concat(grupos.Where(g => g.Secao == secao).Select(static g => g.Ordem)).Min();

    private static SecaoDaInscricaoDto Secao(
        string secao, List<ItemNaSecao> itens, List<GrupoNaSecao> grupos, IReadOnlyDictionary<string, string> titulos) =>
        new(
            secao,
            titulos.GetValueOrDefault(secao),
            [.. itens.Where(i => i.Secao == secao).OrderBy(static i => i.Ordem).Select(static i => i.Item)],
            [.. grupos.Where(g => g.Secao == secao).OrderBy(static g => g.Ordem).Select(static g => g.Grupo)]);

    /// <summary>O que o sistema calculou, com a proteção e a apresentação que a publicação congelou do fato derivado.</summary>
    public static IReadOnlyList<ItemDeRespostaDto> Derivados(IReadOnlyDictionary<string, JsonElement> derivados, FatosDaVersao fatos)
    {
        ArgumentNullException.ThrowIfNull(derivados);
        ArgumentNullException.ThrowIfNull(fatos);

        return
        [
            .. derivados
                .Where(d => fatos.Derivados.ContainsKey(d.Key))
                .OrderBy(static d => d.Key, StringComparer.Ordinal)
                .Select(d => Item(fatos.Derivados[d.Key], d.Value, podeRevelar: false))
                .OfType<ItemDeRespostaDto>(),
        ];
    }

    /// <summary>
    /// Deixa só os itens que a decisão deixa aparecer, sem indício dos omitidos: a seção, o grupo e a ocorrência que ficam
    /// sem item algum também saem.
    /// </summary>
    public static IReadOnlyList<SecaoDaInscricaoDto> Permitidas(IReadOnlyList<SecaoDaInscricaoDto> secoes, IPermissoesDaProjecao decisoes)
    {
        ArgumentNullException.ThrowIfNull(secoes);
        ArgumentNullException.ThrowIfNull(decisoes);

        return
        [
            .. secoes
                .Select(s => new SecaoDaInscricaoDto(
                    s.Codigo,
                    s.Titulo,
                    Visiveis(s.Itens, decisoes),
                    [.. s.Grupos
                        .Select(g => new GrupoDaInscricaoDto(
                            g.Codigo, g.Rotulo,
                            [.. g.Ocorrencias
                                .Select(o => new OcorrenciaDaInscricaoDto(o.Id, Visiveis(o.Itens, decisoes)))
                                .Where(static o => o.Itens.Count > 0)]))
                        .Where(static g => g.Ocorrencias.Count > 0)]))
                .Where(static s => s.Itens.Count > 0 || s.Grupos.Count > 0),
        ];
    }

    /// <summary>Os itens que a decisão deixa aparecer.</summary>
    public static IReadOnlyList<ItemDeRespostaDto> Visiveis(IEnumerable<ItemDeRespostaDto> itens, IPermissoesDaProjecao decisoes)
    {
        ArgumentNullException.ThrowIfNull(itens);
        ArgumentNullException.ThrowIfNull(decisoes);

        return [.. itens.Where(decisoes.ItemSai)];
    }

    /// <remarks>
    /// O CPF sai sempre mascarado, qualquer que seja a classificação congelada: o completo só sai pela revelação. Assim,
    /// reclassificar o CPF não abre o valor a quem consulta os dados da classe.
    /// </remarks>
    private static ItemDeRespostaDto? Item(FatoDoProcesso fato, JsonElement bruto, bool podeRevelar)
    {
        bool identificador = fato.Classificacao == ClassificacaoDeProtecao.Identificador;
        if (identificador && !Respondido(bruto))
        {
            return null;
        }

        return new ItemDeRespostaDto(fato.Classificacao, fato.BaseLegal)
        {
            FatoCodigo = fato.Codigo,
            Rotulo = fato.Rotulo,
            Descricao = fato.Descricao,
            Tipo = fato.Tipo,
            Componente = fato.Componente,
            Valor = identificador || (fato.EhCpf && Respondido(bruto)) ? Mascarado(bruto, fato.EhCpf) : Valor(fato, bruto),
            PodeRevelar = identificador && podeRevelar,
        };
    }

    private static bool Respondido(JsonElement bruto) => bruto.ValueKind switch
    {
        JsonValueKind.Undefined or JsonValueKind.Null => false,
        JsonValueKind.String => !string.IsNullOrWhiteSpace(bruto.GetString()),
        _ => true,
    };

    private static JsonElement Mascarado(JsonElement bruto, bool ehCpf)
    {
        string? texto = bruto.ValueKind == JsonValueKind.String ? bruto.GetString() : null;
        string mascara = ehCpf && texto is not null && Cpf.Criar(texto) is { IsSuccess: true } cpf ? cpf.Value!.Mascarado : MascaraTotal;

        return JsonSerializer.SerializeToElement(mascara);
    }

    private static JsonElement Valor(FatoDoProcesso fato, JsonElement bruto) => fato.Tipo switch
    {
        TiposDeItem.Numero => bruto.ValueKind == JsonValueKind.Number ? JsonSerializer.SerializeToElement(bruto.GetRawText()) : bruto,
        TiposDeItem.Opcao => bruto.ValueKind == JsonValueKind.String ? Opcao(fato, bruto.GetString()!) : bruto,
        TiposDeItem.Opcoes => bruto.ValueKind == JsonValueKind.Array
            ? JsonSerializer.SerializeToElement(bruto.EnumerateArray().Where(static v => v.ValueKind == JsonValueKind.String).Select(v => Opcao(fato, v.GetString()!)))
            : bruto,
        TiposDeItem.Endereco => EnderecoDeclarado.TentarLer(bruto, out EnderecoDeclarado endereco) ? Endereco(endereco) : bruto,
        _ => bruto,
    };

    private static JsonElement Opcao(FatoDoProcesso fato, string codigo) =>
        JsonSerializer.SerializeToElement(new { codigo, rotulo = fato.Opcoes.GetValueOrDefault(codigo, codigo) });

    private static JsonElement Endereco(EnderecoDeclarado e) => JsonSerializer.SerializeToElement(new
    {
        cep = e.Cep,
        logradouro = e.Logradouro,
        numero = e.Numero,
        complemento = e.Complemento,
        bairro = e.Bairro,
        municipio = e.CidadeNome,
        municipioCodigoIbge = e.CidadeCodigoIbge,
        uf = e.CidadeUf,
        ufNome = UnidadesFederativas.PorSigla(e.CidadeUf)?.Nome,
    });
}
