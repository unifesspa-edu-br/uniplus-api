namespace Unifesspa.UniPlus.Publicacoes.Domain.Entities;

using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Domain.Errors;
using Unifesspa.UniPlus.Publicacoes.Domain.ValueObjects;

internal sealed class ValidacaoDoTipoAtoPublicado(DeclaracaoDoTipoAtoPublicado declaracao)
{
    private static readonly CampoDeCodigo CampoCodigo = new(
        "Código do tipo de ato", TipoAtoPublicado.TamanhoMaximoDoCodigo, TipoAtoPublicadoErrorCodes.CodigoObrigatorio, TipoAtoPublicadoErrorCodes.CodigoFormato);
    private readonly List<FieldError> _erros = [];
    private string? _nome;
    private string? _baseLegal;

    public Result<CamposDoTipoAtoPublicado> Resolver()
    {
        ConferirCodigo();
        ConferirNome();
        ConferirBaseLegal();
        ConferirVigencia();
        ConferirResultadoIrreversivel();
        return _erros.Count > 0
            ? Result<CamposDoTipoAtoPublicado>.ValidationFailure(_erros)
            : Result<CamposDoTipoAtoPublicado>.Success(Montar());
    }

    private CamposDoTipoAtoPublicado Montar() => new(
        declaracao.Codigo!, _nome!, declaracao.CongelaConfiguracao, declaracao.UnicoPorObjeto,
        declaracao.EfeitoIrreversivel, declaracao.EhResultado, declaracao.PublicavelComoAvulso,
        declaracao.ExigeAtoCorrigido, declaracao.VigenciaInicio, declaracao.VigenciaFim, _baseLegal);

    private void ConferirCodigo()
    {
        if (CampoCodigo.Recusa(declaracao.Codigo, "codigo") is { } recusa)
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
        if (declaracao.VigenciaFim is { } fim && fim <= declaracao.VigenciaInicio)
        {
            _erros.Add(new("vigenciaFim", new DomainError(
                TipoAtoPublicadoErrorCodes.VigenciaFimAnteriorAoInicio,
                "Fim da vigência é exclusivo e deve ser posterior ao início.")));
        }
    }

    private void ConferirResultadoIrreversivel()
    {
        if (declaracao.EhResultado && declaracao.EfeitoIrreversivel)
        {
            _erros.Add(new("efeitoIrreversivel", new DomainError(
                TipoAtoPublicadoErrorCodes.ResultadoComEfeitoIrreversivel,
                "Um tipo de ato de resultado não pode ter efeito irreversível: é no resultado preliminar "
                + "que se abre o prazo de recurso, e não cabe recurso contra ato irreversível. O efeito "
                + "irreversível fica com o ato que decorre do resultado, como a convocação. Desmarque uma das duas opções.")));
        }
    }
}
