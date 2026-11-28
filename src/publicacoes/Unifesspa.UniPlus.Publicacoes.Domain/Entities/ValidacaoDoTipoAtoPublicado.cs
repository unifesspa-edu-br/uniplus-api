namespace Unifesspa.UniPlus.Publicacoes.Domain.Entities;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Domain.Errors;
using Unifesspa.UniPlus.Publicacoes.Domain.ValueObjects;

internal sealed class ValidacaoDoTipoAtoPublicado(DeclaracaoDoTipoAtoPublicado declaracao)
{
    private readonly List<FieldError> _erros = [];
    private string? _nome;
    private string? _baseLegal;

    public Result<CamposDoTipoAtoPublicado> Resolver()
    {
        ConferirCodigo();
        ConferirNome();
        ConferirAtributos();
        ConferirInicioDaVigencia();
        ConferirBaseLegal();
        ConferirVigencia();
        ConferirResultadoIrreversivel();
        return _erros.Count > 0
            ? Result<CamposDoTipoAtoPublicado>.ValidationFailure(_erros)
            : Result<CamposDoTipoAtoPublicado>.Success(Montar());
    }

    // Só roda sem recusas: os campos anuláveis já foram exigidos, então o valor padrão nunca é usado.
    private CamposDoTipoAtoPublicado Montar() => new(
        declaracao.Codigo!, _nome!, declaracao.CongelaConfiguracao.GetValueOrDefault(),
        declaracao.UnicoPorObjeto.GetValueOrDefault(), declaracao.EfeitoIrreversivel.GetValueOrDefault(),
        declaracao.EhResultado.GetValueOrDefault(), declaracao.PublicavelComoAvulso.GetValueOrDefault(),
        declaracao.ExigeAtoCorrigido.GetValueOrDefault(), declaracao.VigenciaInicio.GetValueOrDefault(),
        declaracao.VigenciaFim, _baseLegal);

    private void ConferirCodigo()
    {
        if (TipoAtoPublicado.CampoDoCodigo.Recusa(declaracao.Codigo, "codigo") is { } recusa)
        {
            _erros.Add(recusa);
        }
    }

    private void ConferirNome()
    {
        _nome = declaracao.Nome?.Trim();
        if (RecusaDoNome(_nome) is { } recusa)
        {
            _erros.Add(new("nome", recusa));
        }
    }

    private static DomainError? RecusaDoNome(string? nome) =>
        string.IsNullOrEmpty(nome)
            ? new(TipoAtoPublicadoErrorCodes.NomeObrigatorio, "Nome do tipo de ato é obrigatório.")
            : nome.Length is < 2 or > 200
                ? new(TipoAtoPublicadoErrorCodes.NomeTamanho, "Nome do tipo de ato deve ter entre 2 e 200 caracteres.")
                : null;

    private void ConferirAtributos()
    {
        ExigirInformado(declaracao.CongelaConfiguracao, "congelaConfiguracao", "Informe se o ato deste tipo congela a configuração.");
        ExigirInformado(declaracao.UnicoPorObjeto, "unicoPorObjeto", "Informe se o objeto admite um único ato vivo deste tipo.");
        ExigirInformado(declaracao.EfeitoIrreversivel, "efeitoIrreversivel", "Informe se a publicação de um ato deste tipo é irreversível.");
        ExigirInformado(declaracao.EhResultado, "ehResultado", "Informe se um ato deste tipo determina a situação do candidato.");
        ExigirInformado(declaracao.PublicavelComoAvulso, "publicavelComoAvulso", "Informe se um ato deste tipo pode ser publicado como documento avulso.");
        ExigirInformado(declaracao.ExigeAtoCorrigido, "exigeAtoCorrigido", "Informe se um ato deste tipo corrige outro ato publicado.");
    }

    private void ConferirInicioDaVigencia() =>
        ExigirInformado(declaracao.VigenciaInicio, "vigenciaInicio", "Informe o primeiro dia de vigência do tipo de ato.");

    private void ExigirInformado<T>(T? valor, string campo, string mensagem)
        where T : struct
    {
        if (valor is null)
        {
            _erros.Add(new(campo, new DomainError(CodigosDeValidacao.Obrigatorio, mensagem)));
        }
    }

    private void ConferirBaseLegal()
    {
        _baseLegal = string.IsNullOrWhiteSpace(declaracao.BaseLegal) ? null : declaracao.BaseLegal.Trim();
        if (_baseLegal?.Length > 500)
        {
            _erros.Add(new("baseLegal", new DomainError(
                TipoAtoPublicadoErrorCodes.BaseLegalTamanho, "Base legal deve ter no máximo 500 caracteres.")));
        }
    }

    private void ConferirVigencia()
    {
        if (declaracao.VigenciaInicio is { } inicio && declaracao.VigenciaFim is { } fim && fim <= inicio)
        {
            _erros.Add(new("vigenciaFim", new DomainError(
                TipoAtoPublicadoErrorCodes.VigenciaFimAnteriorAoInicio,
                "Fim da vigência é exclusivo e deve ser posterior ao início.")));
        }
    }

    private void ConferirResultadoIrreversivel()
    {
        if (declaracao.EhResultado is true && declaracao.EfeitoIrreversivel is true)
        {
            _erros.Add(new("efeitoIrreversivel", new DomainError(
                TipoAtoPublicadoErrorCodes.ResultadoComEfeitoIrreversivel,
                "Um tipo de ato de resultado não pode ter efeito irreversível: é no resultado preliminar "
                + "que se abre o prazo de recurso, e não cabe recurso contra ato irreversível. O efeito "
                + "irreversível fica com o ato que decorre do resultado, como a convocação. Desmarque uma das duas opções.")));
        }
    }
}
