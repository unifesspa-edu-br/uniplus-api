namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A base legal do Fator de Atenuação das Desigualdades de Acesso (FADA) como o processo a congela: o identificador e o código do cadastro de Configuração, mais
/// tipo, identificação, descrição e municípios do momento da gravação. Chega já resolvida e validada pela leitura do cadastro
/// (ou lida do envelope publicado), e por isso não é revalidada aqui.
/// </summary>
/// <param name="Id">Identificador da base legal no cadastro; é o que decide.</param>
/// <param name="Codigo">Código estável da base legal, o que o documento de configuração usa para referenciá-la.</param>
/// <param name="TipoInstrumento">Código canônico do tipo do instrumento normativo.</param>
/// <param name="Identificacao">Identificação do ato (ex.: "Portaria Unifesspa nº 2514/2023").</param>
/// <param name="Descricao">Descrição da base legal.</param>
/// <param name="Municipios">Municípios abrangidos pela base legal no momento da gravação.</param>
public sealed record BaseLegalCongelada(
    Guid Id,
    string Codigo,
    string TipoInstrumento,
    string Identificacao,
    string Descricao,
    IReadOnlyList<(string CodigoIbge, string Nome, string Uf)> Municipios);
