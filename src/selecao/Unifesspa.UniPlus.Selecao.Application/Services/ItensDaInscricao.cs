namespace Unifesspa.UniPlus.Selecao.Application.Services;

using System.Text.Json;

using Unifesspa.UniPlus.Authorization.Projecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O resolvedor da inscrição: para cada resposta, a proteção que o processo congelou (classificação e base legal), o
/// rótulo, a descrição, o tipo e o componente, e o valor na forma do tipo (ADR-0153). É o único lugar que responde "qual é
/// a proteção deste item" para a consulta administrativa; a permissão que decide a saída é do projetor.
/// </summary>
/// <remarks>
/// O identificador sai mascarado e classificado como pessoal, lido da resposta identificadora sob a decisão pessoal: a
/// fonte sem a transformação nunca sai. A classificação que não se resolve vale como sensível.
/// </remarks>
internal static class ItensDaInscricao
{
    /// <summary>A máscara total dos identificadores que não são o CPF, até a proteção de dados definir a própria.</summary>
    public const string MascaraTotal = "•••••••";

    /// <summary>As respostas do formulário agrupadas por seção, na ordem de coleta, com todos os itens, sem decidir a saída.</summary>
    public static IReadOnlyList<SecaoDaInscricaoDto> Secoes(Inscricao inscricao, FatosDaVersao fatos, bool podeRevelar)
    {
        ArgumentNullException.ThrowIfNull(inscricao);
        ArgumentNullException.ThrowIfNull(fatos);

        List<(int Ordem, string Secao, ItemDeRespostaDto Item)> itens = [];
        foreach ((string codigo, JsonElement valor) in inscricao.Respostas)
        {
            if (fatos.Coletados.TryGetValue(codigo, out FatoDoProcesso? fato))
            {
                itens.Add((fato.Ordem, fato.Secao ?? string.Empty, Item(fato, valor, podeRevelar)));
            }
        }

        List<(int Ordem, string Secao, GrupoDaInscricaoDto Grupo)> grupos = [];
        foreach (GrupoDoProcesso grupo in fatos.Grupos)
        {
            if (!inscricao.Grupos.TryGetValue(grupo.Codigo, out IReadOnlyList<OcorrenciaDeGrupoInscricao>? ocorrencias))
            {
                continue;
            }

            List<OcorrenciaDaInscricaoDto> daOcorrencia =
            [
                .. ocorrencias.Select(o => new OcorrenciaDaInscricaoDto(
                    o.Id,
                    [.. o.Respostas
                        .Where(r => grupo.Subitens.ContainsKey(r.Key))
                        .OrderBy(r => grupo.Subitens[r.Key].Ordem)
                        .Select(r => Item(grupo.Subitens[r.Key], r.Value, podeRevelar))])),
            ];
            grupos.Add((grupo.Ordem, grupo.Secao ?? string.Empty, new GrupoDaInscricaoDto(grupo.Codigo, grupo.Rotulo, daOcorrencia)));
        }

        return
        [
            .. itens.Select(static i => i.Secao).Concat(grupos.Select(static g => g.Secao)).Distinct(StringComparer.Ordinal)
                .Select(secao => (
                    Ordem: itens.Where(i => i.Secao == secao).Select(static i => i.Ordem)
                        .Concat(grupos.Where(g => g.Secao == secao).Select(static g => g.Ordem)).Min(),
                    Dto: new SecaoDaInscricaoDto(
                        secao,
                        fatos.TitulosDasSecoes.GetValueOrDefault(secao),
                        [.. itens.Where(i => i.Secao == secao).OrderBy(static i => i.Ordem).Select(static i => i.Item)],
                        [.. grupos.Where(g => g.Secao == secao).OrderBy(static g => g.Ordem).Select(static g => g.Grupo)])))
                .OrderBy(static s => s.Ordem)
                .Select(static s => s.Dto),
        ];
    }

    /// <summary>O que o sistema calculou, com a proteção e a apresentação que a publicação congelou do fato derivado.</summary>
    public static IReadOnlyList<ItemDeRespostaDto> Derivados(Inscricao inscricao, FatosDaVersao fatos)
    {
        ArgumentNullException.ThrowIfNull(inscricao);
        ArgumentNullException.ThrowIfNull(fatos);

        return
        [
            .. inscricao.Derivados
                .Where(d => fatos.Derivados.ContainsKey(d.Key))
                .OrderBy(static d => d.Key, StringComparer.Ordinal)
                .Select(d => Item(fatos.Derivados[d.Key], d.Value, podeRevelar: false)),
        ];
    }

    /// <summary>
    /// Deixa só os itens que a decisão permite, sem indício dos omitidos: a seção, o grupo e a ocorrência que ficam sem
    /// item algum também saem.
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
                    [.. s.Itens.Where(decisoes.Permite)],
                    [.. s.Grupos
                        .Select(g => new GrupoDaInscricaoDto(
                            g.Codigo, g.Rotulo,
                            [.. g.Ocorrencias
                                .Select(o => new OcorrenciaDaInscricaoDto(o.Id, [.. o.Itens.Where(decisoes.Permite)]))
                                .Where(static o => o.Itens.Count > 0)]))
                        .Where(static g => g.Ocorrencias.Count > 0)]))
                .Where(static s => s.Itens.Count > 0 || s.Grupos.Count > 0),
        ];
    }

    private static ItemDeRespostaDto Item(FatoDoProcesso fato, JsonElement bruto, bool podeRevelar)
    {
        bool identificador = fato.Classificacao == ClassificacaoDeProtecao.Identificador;
        return new ItemDeRespostaDto(
            identificador ? ClassificacaoDeProtecao.Pessoal : fato.Classificacao,
            fato.BaseLegal)
        {
            FatoCodigo = fato.Codigo,
            Rotulo = fato.Rotulo,
            Descricao = fato.Descricao,
            Tipo = fato.Tipo,
            Componente = fato.Componente,
            Valor = identificador ? Mascarado(bruto, fato.EhCpf) : Valor(fato, bruto),
            PodeRevelar = identificador && podeRevelar,
        };
    }

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
    });
}
