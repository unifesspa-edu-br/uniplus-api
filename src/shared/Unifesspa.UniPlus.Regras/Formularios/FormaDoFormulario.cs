namespace Unifesspa.UniPlus.Regras.Formularios;

using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;

/// <summary>
/// A forma de um item do formulário, a mesma no formulário do processo e no modelo: o código do
/// fato, a ordem, o rótulo, o tipo de campo, a ajuda, o formato e as restrições de valor.
/// Acumula toda violação independente em vez de parar na primeira (ADR-0125).
/// </summary>
public static class FormaDoItem
{
    public const int FatoCodigoMaxLength = 60;

    /// <summary>A mesma grandeza de um nome de cadastro curto; o envelope aplica o mesmo limite.</summary>
    public const int RotuloMaxLength = 300;

    public const int FormatoMaxLength = 30;

    /// <summary>A mesma grandeza do formato — token canônico curto (o maior hoje é "IDENTIFICADOR").</summary>
    public const int ClassificacaoProtecaoMaxLength = 30;

    /// <summary>O token canônico da hipótese legal de tratamento copiada do catálogo.</summary>
    public const int HipoteseLegalMaxLength = 40;

    /// <summary>A finalidade do tratamento copiada do catálogo.</summary>
    public const int FinalidadeTratamentoMaxLength = 500;

    /// <summary>A descrição do fato, copiada do catálogo, que explica o dado a quem analisa a inscrição.</summary>
    public const int DescricaoDoFatoMaxLength = 1000;

    /// <summary>O token canônico do componente de exibição copiado do catálogo.</summary>
    public const int ComponenteExibicaoMaxLength = 20;

    public const int AjudaMaxLength = 1000;

    /// <summary>A mensagem que explica ao candidato por que a resposta impede a inscrição.</summary>
    public const int MensagemDoImpedimentoMaxLength = 500;

    /// <summary>As casas decimais dos limites da faixa numérica, as mesmas com que o edital os congela.</summary>
    public const int CasasDecimaisDaFaixa = 4;

    /// <summary>
    /// Teto de itens de um formulário. O maior formulário do edital de Medicina 2027 tem algumas
    /// dezenas de campos; o teto dá folga larga sem deixar uma entrada pequena gerar resposta
    /// desproporcional, porque acima dele a lista é recusada inteira, sem um erro por item.
    /// </summary>
    public const int MaximoDeItens = 200;

    /// <summary>
    /// O que o teto do formulário conta: cada item, cada grupo e cada campo de grupo, dados os
    /// itens e a quantidade de campos de cada grupo.
    /// </summary>
    public static int QuantidadeNoTeto(int itens, IEnumerable<int> camposPorGrupo)
    {
        ArgumentNullException.ThrowIfNull(camposPorGrupo);
        return itens + camposPorGrupo.Sum(static campos => 1 + campos);
    }

    /// <summary>
    /// A quantidade de itens não depende do catálogo: existe separada para quem recebe a lista
    /// recusá-la acima do teto antes de ler o catálogo e de conferir item a item.
    /// </summary>
    public static List<FieldError> ValidarQuantidade(int quantidade)
    {
        List<FieldError> erros = [];
        if (quantidade > MaximoDeItens)
        {
            erros.Add(new("itens", new DomainError(
                ItemFormularioErrorCodes.ItensEmExcesso, $"O formulário admite no máximo {MaximoDeItens} itens; vieram {quantidade}.")));
        }

        return erros;
    }

    /// <summary>
    /// Os campos que não dependem do catálogo nem das regras — código do fato, ordem e rótulo: existe separada para quem
    /// recebe a lista inteira conferir a forma de todos os itens antes de resolver o catálogo, e
    /// assim um código vazio não cai num "fato desconhecido", menos específico.
    /// </summary>
    public static List<FieldError> ValidarFormaBasica(string? fatoCodigo, int ordem, string? rotulo) =>
        [.. RecusasDoCodigoDoFato(fatoCodigo), .. RecusasDaOrdem(ordem), .. RecusasDoRotulo(rotulo)];

    private static IEnumerable<FieldError> RecusasDoCodigoDoFato(string? fatoCodigo)
    {
        if (string.IsNullOrWhiteSpace(fatoCodigo))
        {
            yield return new("fatoCodigo", new DomainError(ItemFormularioErrorCodes.FatoCodigoObrigatorio, "O código do fato do item é obrigatório."));
        }
        else if (fatoCodigo.Trim().Length > FatoCodigoMaxLength)
        {
            yield return new("fatoCodigo", new DomainError(
                ItemFormularioErrorCodes.FatoCodigoTamanho, $"O código do fato do item deve ter no máximo {FatoCodigoMaxLength} caracteres."));
        }
    }

    private static IEnumerable<FieldError> RecusasDaOrdem(int ordem)
    {
        if (ordem < 0)
        {
            yield return new("ordem", new DomainError(ItemFormularioErrorCodes.OrdemInvalida, "A ordem do item não pode ser negativa."));
        }
    }

    private static IEnumerable<FieldError> RecusasDoRotulo(string? rotulo)
    {
        if (string.IsNullOrWhiteSpace(rotulo))
        {
            yield return new("rotulo", new DomainError(ItemFormularioErrorCodes.RotuloObrigatorio, "O rótulo do item é obrigatório."));
        }
        else if (rotulo.Trim().Length > RotuloMaxLength)
        {
            yield return new("rotulo", new DomainError(
                ItemFormularioErrorCodes.RotuloTamanho, $"O rótulo do item deve ter no máximo {RotuloMaxLength} caracteres."));
        }
    }

    /// <summary>
    /// A forma inteira do item: a básica, a ajuda, o formato (só o campo de texto tem), nenhuma
    /// regra citando o próprio fato e as restrições cabendo no campo. A autorreferência é ciclo de
    /// comprimento um, recusada aqui com um erro específico, sem precisar conhecer os outros itens.
    /// O impedimento é a exceção: cada cláusula dele cita a resposta do próprio campo.
    /// </summary>
    public static List<FieldError> Conferir(string? fatoCodigo, int ordem, ApresentacaoDoCampo apresentacao, string? formato, RegrasDoCampo regras)
    {
        ArgumentNullException.ThrowIfNull(apresentacao);
        ArgumentNullException.ThrowIfNull(regras);

        return
        [
            .. ValidarFormaBasica(fatoCodigo, ordem, apresentacao.Rotulo),
            .. apresentacao.ConferirAjuda(),
            .. RecusasDoFormato(apresentacao.TipoRenderizacao, formato),
            .. regras.ConferirNoCampo(fatoCodigo?.Trim() ?? string.Empty, apresentacao.TipoRenderizacao),
        ];
    }

    private static IEnumerable<FieldError> RecusasDoFormato(TipoRenderizacao tipoRenderizacao, string? formato)
    {
        string? formatoNormalizado = TextoOpcional(formato);
        if ((tipoRenderizacao == TipoRenderizacao.Texto) != (formatoNormalizado is not null)
            || formatoNormalizado is { Length: > FormatoMaxLength })
        {
            yield return new("formato", new DomainError(
                ItemFormularioErrorCodes.FormatoIncoerente,
                $"O campo de texto tem o formato do fato no catálogo, com no máximo {FormatoMaxLength} caracteres; os demais campos não têm formato."));
        }
    }

    /// <summary>Se o tipo de restrição se aplica ao tipo de campo.</summary>
    public static bool RestricaoCabeNoCampo(TipoRestricaoValor restricao, TipoRenderizacao campo) =>
        restricao switch
        {
            TipoRestricaoValor.FaixaNumerica => campo == TipoRenderizacao.Numero,
            TipoRestricaoValor.TamanhoTexto => campo == TipoRenderizacao.Texto,
            TipoRestricaoValor.OpcoesPermitidas or TipoRestricaoValor.OpcoesDasRespostas => campo.EhSelecao(),
            TipoRestricaoValor.MunicipiosDaUf => campo == TipoRenderizacao.Municipio,
            TipoRestricaoValor.FaixaDeData => campo == TipoRenderizacao.Data,
            TipoRestricaoValor.Nenhuma => false,
            _ => throw new ArgumentOutOfRangeException(nameof(restricao), restricao, "Tipo de restrição desconhecido."),
        };

    /// <summary>O texto aparado, ou nulo quando vazio — a forma em que o item guarda ajuda e formato.</summary>
    public static string? TextoOpcional(string? texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
}

/// <summary>
/// A forma de um grupo repetível do formulário, a mesma no processo e no modelo (UNI-REQ-0146): o
/// código, a ordem, o rótulo, as ocorrências e os campos de cada ocorrência.
/// Acumula toda violação independente em vez de parar na primeira (ADR-0125).
/// </summary>
public static class FormaDoGrupo
{
    public const int CodigoMaxLength = 60;

    /// <summary>Teto de campos de cada ocorrência: um membro da família tem poucos dados declarados.</summary>
    public const int MaximoDeSubitens = 30;

    /// <summary>O grupo tem de um até o teto de campos por ocorrência.</summary>
    public static bool QuantidadeDeCamposValida(int quantidade) => quantidade is >= 1 and <= MaximoDeSubitens;

    /// <summary>
    /// Código e rótulo obrigatórios e limitados, ordem não negativa, ocorrências coerentes, ao menos um
    /// campo e no máximo o teto, nenhum campo com seção própria — o campo aparece onde o grupo aparece
    /// — e nenhuma regra do grupo citando o próprio grupo ou os campos dele — fora da ocorrência, eles
    /// não têm valor único.
    /// </summary>
    public static List<FieldError> Conferir(
        string? codigo,
        int ordem,
        string? rotulo,
        OcorrenciasDoGrupo ocorrencias,
        IReadOnlyList<(string? FatoCodigo, string? EtapaCodigo, bool TemImpedimento)> subitens,
        IEnumerable<string> fatosCitadosPelaExibicao,
        Obrigatoriedade obrigatoriedade)
    {
        ArgumentNullException.ThrowIfNull(ocorrencias);
        ArgumentNullException.ThrowIfNull(subitens);
        ArgumentNullException.ThrowIfNull(fatosCitadosPelaExibicao);
        ArgumentNullException.ThrowIfNull(obrigatoriedade);

        return
        [
            .. RecusasDoCodigo(codigo), .. RecusasDaOrdem(ordem), .. RecusasDoRotulo(rotulo), .. ocorrencias.Conferir(),
            .. RecusasDaQuantidadeDeCampos(subitens.Count), .. RecusasDosCampos(subitens),
            .. RecusasDeRegraQueCitaOGrupo(codigo?.Trim() ?? string.Empty, subitens, fatosCitadosPelaExibicao, obrigatoriedade),
        ];
    }

    private static IEnumerable<FieldError> RecusasDoCodigo(string? codigo)
    {
        if ((codigo?.Trim() ?? string.Empty).Length is 0 or > CodigoMaxLength)
        {
            yield return new("codigo", new DomainError(
                GrupoFormularioErrorCodes.CodigoInvalido, $"O código do grupo é obrigatório e tem no máximo {CodigoMaxLength} caracteres."));
        }
    }

    private static IEnumerable<FieldError> RecusasDaOrdem(int ordem)
    {
        if (ordem < 0)
        {
            yield return new("ordem", new DomainError(GrupoFormularioErrorCodes.OrdemInvalida, "A ordem do grupo não pode ser negativa."));
        }
    }

    private static IEnumerable<FieldError> RecusasDoRotulo(string? rotulo)
    {
        if ((rotulo?.Trim() ?? string.Empty).Length is 0 or > FormaDoItem.RotuloMaxLength)
        {
            yield return new("rotulo", new DomainError(
                GrupoFormularioErrorCodes.RotuloInvalido,
                $"O rótulo do grupo é obrigatório e tem no máximo {FormaDoItem.RotuloMaxLength} caracteres."));
        }
    }

    private static IEnumerable<FieldError> RecusasDaQuantidadeDeCampos(int quantidade)
    {
        if (!QuantidadeDeCamposValida(quantidade))
        {
            yield return new("subitens", new DomainError(
                GrupoFormularioErrorCodes.SubitensForaDoLimite, $"O grupo tem de um a {MaximoDeSubitens} campos por ocorrência."));
        }
    }

    private static IEnumerable<FieldError> RecusasDosCampos(IReadOnlyList<(string? FatoCodigo, string? EtapaCodigo, bool TemImpedimento)> subitens) =>
        subitens.SelectMany(static (subitem, indice) => RecusasDoCampo(subitem, indice));

    private static IEnumerable<FieldError> RecusasDoCampo((string? FatoCodigo, string? EtapaCodigo, bool TemImpedimento) subitem, int indice)
    {
        if (subitem.EtapaCodigo is not null)
        {
            yield return new($"subitens[{indice}].etapaCodigo", new DomainError(
                GrupoFormularioErrorCodes.CampoComSecaoPropria,
                $"O campo '{subitem.FatoCodigo}' segue a seção do grupo e não declara seção própria."));
        }

        if (subitem.TemImpedimento)
        {
            yield return new($"subitens[{indice}].impedimento", new DomainError(
                GrupoFormularioErrorCodes.CampoComImpedimento,
                $"O campo '{subitem.FatoCodigo}' é de membro: o impedimento é da inscrição do candidato, não de um membro."));
        }
    }

    private static IEnumerable<FieldError> RecusasDeRegraQueCitaOGrupo(
        string codigoAparado,
        IReadOnlyList<(string? FatoCodigo, string? EtapaCodigo, bool TemImpedimento)> subitens,
        IEnumerable<string> fatosCitadosPelaExibicao,
        Obrigatoriedade obrigatoriedade)
    {
        HashSet<string> doGrupo = new(subitens.Select(static s => s.FatoCodigo).OfType<string>().Append(codigoAparado), StringComparer.Ordinal);
        return
        [
            .. RecusasDaRegraQueCitaOGrupo("exibicao", fatosCitadosPelaExibicao, doGrupo),
            .. RecusasDaRegraQueCitaOGrupo("predicadoObrigatoriedade", obrigatoriedade.FatosCitados, doGrupo),
        ];
    }

    private static IEnumerable<FieldError> RecusasDaRegraQueCitaOGrupo(string campo, IEnumerable<string> citados, HashSet<string> doGrupo)
    {
        if (citados.FirstOrDefault(doGrupo.Contains) is { } citado)
        {
            yield return new(campo, new DomainError(
                GrupoFormularioErrorCodes.RegraAutorreferente,
                $"Uma regra do grupo cita '{citado}', do próprio grupo — a exibição e a obrigatoriedade do grupo se decidem antes das ocorrências."));
        }
    }
}

/// <summary>O cabeçalho do formulário, o mesmo no processo e no modelo: a finalidade e o título.</summary>
public static class FormaDoCabecalho
{
    public const int TituloMaxLength = 300;

    /// <summary>A finalidade é uma das três do formulário, e o título, quando há, cabe no limite.</summary>
    public static List<FieldError> Validar(FinalidadeFormulario finalidade, string? titulo) =>
        [.. ValidarFinalidade(finalidade), .. ValidarTitulo(titulo)];

    /// <summary>A finalidade é inscrição, isenção de taxa ou habilitação.</summary>
    public static List<FieldError> ValidarFinalidade(FinalidadeFormulario finalidade) =>
        finalidade == FinalidadeFormulario.Nenhuma || !Enum.IsDefined(finalidade)
            ? [new("finalidade", new DomainError(EstruturaFormularioErrorCodes.FinalidadeInvalida,
                "A finalidade do formulário é inscrição, isenção de taxa ou habilitação."))]
            : [];

    /// <summary>O título, quando há, cabe no limite.</summary>
    public static List<FieldError> ValidarTitulo(string? titulo) =>
        FormaDoItem.TextoOpcional(titulo) is { Length: > TituloMaxLength }
            ? [new("titulo", new DomainError(EstruturaFormularioErrorCodes.TituloTamanho,
                $"O título do formulário deve ter no máximo {TituloMaxLength} caracteres."))]
            : [];
}

/// <summary>A forma de uma etapa do formulário, a mesma no processo e no modelo.</summary>
public static class FormaDaEtapa
{
    public const int CodigoMaxLength = 60;
    public const int TituloMaxLength = 300;
    public const int TextoMaxLength = 2000;

    /// <summary>
    /// Código e título obrigatórios e limitados, ordem não negativa, descrição e aviso limitados, e
    /// exibição condicional só na seção — o bloco de sistema aparece sempre.
    /// </summary>
    public static List<FieldError> Conferir(
        string? codigo, int ordem, TipoEtapaFormulario tipo, string? titulo, string? descricao, string? aviso, bool temExibicao) =>
    [
        .. RecusasDoCodigo(codigo),
        .. RecusasDaOrdem(ordem),
        .. RecusasDoTitulo(titulo),
        .. RecusasDaDescricao(descricao),
        .. RecusasDoAviso(aviso),
        .. RecusasDaExibicao(tipo, temExibicao),
    ];

    private static IEnumerable<FieldError> RecusasDoCodigo(string? codigo)
    {
        if ((codigo?.Trim() ?? string.Empty).Length is 0 or > CodigoMaxLength)
        {
            yield return new("codigo", new DomainError(
                EstruturaFormularioErrorCodes.EtapaCodigoInvalido,
                $"O código da etapa é obrigatório e tem no máximo {CodigoMaxLength} caracteres."));
        }
    }

    private static IEnumerable<FieldError> RecusasDaOrdem(int ordem)
    {
        if (ordem < 0)
        {
            yield return new("ordem", new DomainError(EstruturaFormularioErrorCodes.EtapaOrdemInvalida, "A ordem da etapa não pode ser negativa."));
        }
    }

    private static IEnumerable<FieldError> RecusasDoTitulo(string? titulo)
    {
        if ((titulo?.Trim() ?? string.Empty).Length is 0 or > TituloMaxLength)
        {
            yield return new("titulo", new DomainError(
                EstruturaFormularioErrorCodes.EtapaTituloInvalido,
                $"O título da etapa é obrigatório e tem no máximo {TituloMaxLength} caracteres."));
        }
    }

    private static IEnumerable<FieldError> RecusasDaDescricao(string? descricao)
    {
        if (FormaDoItem.TextoOpcional(descricao) is { Length: > TextoMaxLength })
        {
            yield return new("descricao", new DomainError(
                EstruturaFormularioErrorCodes.EtapaTextoTamanho, $"A descrição da etapa tem no máximo {TextoMaxLength} caracteres."));
        }
    }

    private static IEnumerable<FieldError> RecusasDoAviso(string? aviso)
    {
        if (FormaDoItem.TextoOpcional(aviso) is { Length: > TextoMaxLength })
        {
            yield return new("aviso", new DomainError(
                EstruturaFormularioErrorCodes.EtapaTextoTamanho, $"O aviso da etapa tem no máximo {TextoMaxLength} caracteres."));
        }
    }

    private static IEnumerable<FieldError> RecusasDaExibicao(TipoEtapaFormulario tipo, bool temExibicao)
    {
        if (temExibicao && tipo != TipoEtapaFormulario.Secao)
        {
            yield return new("exibicao", new DomainError(
                EstruturaFormularioErrorCodes.ExibicaoForaDeSecao,
                "Só a seção tem exibição condicional; o bloco de sistema aparece sempre."));
        }
    }
}

/// <summary>A forma de um termo exigido pelo formulário, a mesma no processo e no modelo.</summary>
public static class FormaDoTermo
{
    public const int CodigoMaxLength = 60;

    /// <summary>A forma do termo que não depende do catálogo: confere todo o payload antes de ler as versões.</summary>
    public static List<FieldError> ValidarFormaBasica(string? codigo, int ordem) =>
        [.. RecusasDoCodigo(codigo), .. RecusasDaOrdem(ordem)];

    private static IEnumerable<FieldError> RecusasDoCodigo(string? codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo))
        {
            yield return new("codigo", new DomainError(TermoFormularioErrorCodes.CodigoObrigatorio, "O código do termo exigido é obrigatório."));
        }
        else if (codigo.Trim().Length > CodigoMaxLength)
        {
            yield return new("codigo", new DomainError(
                TermoFormularioErrorCodes.CodigoTamanho, $"O código do termo exigido deve ter no máximo {CodigoMaxLength} caracteres."));
        }
    }

    private static IEnumerable<FieldError> RecusasDaOrdem(int ordem)
    {
        if (ordem < 0)
        {
            yield return new("ordem", new DomainError(TermoFormularioErrorCodes.OrdemInvalida, "A ordem do termo não pode ser negativa."));
        }
    }

    /// <summary>O código em NFC; o que não se normaliza é comparado como veio, e a recusa dele é de quem guarda o texto.</summary>
    private static string EmNfc(string codigo) => TextoNormalizavel.TentarNormalizar(codigo, out string normalizado) ? normalizado : codigo;

    /// <summary>
    /// Código e ordem únicos entre os termos do formulário, com o código na forma que o termo guarda
    /// (aparado, em NFC). Termo ausente ou sem código não entra na conferência do código — a recusa
    /// dele é a de forma.
    /// </summary>
    public static List<FieldError> ConferirUnicidade(IReadOnlyList<(string? Codigo, int Ordem)?> termos)
    {
        ArgumentNullException.ThrowIfNull(termos);

        List<FieldError> erros = [];
        HashSet<string> codigos = new(StringComparer.Ordinal);
        HashSet<int> ordens = [];
        for (int i = 0; i < termos.Count; i++)
        {
            if (termos[i] is { } termo)
            {
                erros.AddRange([.. RecusasDeCodigoDuplicado(i, termo.Codigo, codigos), .. RecusasDeOrdemDuplicada(i, termo.Ordem, ordens)]);
            }
        }

        return erros;
    }

    private static IEnumerable<FieldError> RecusasDeCodigoDuplicado(int indice, string? codigo, HashSet<string> codigos)
    {
        if (!string.IsNullOrWhiteSpace(codigo) && !codigos.Add(EmNfc(codigo.Trim())))
        {
            yield return new($"termos[{indice}].codigo", new DomainError(
                TermoFormularioErrorCodes.CodigoDuplicado, $"O código '{codigo.Trim()}' aparece em mais de um termo do formulário."));
        }
    }

    private static IEnumerable<FieldError> RecusasDeOrdemDuplicada(int indice, int ordem, HashSet<int> ordens)
    {
        if (!ordens.Add(ordem))
        {
            yield return new($"termos[{indice}].ordem", new DomainError(
                TermoFormularioErrorCodes.OrdemDuplicada, "Dois termos do formulário têm a mesma ordem."));
        }
    }
}
