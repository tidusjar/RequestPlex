import { Component, OnInit, Input, ViewChild } from "@angular/core";
import { DiscoverOption, IDiscoverCardResult } from "../../interfaces";
import { ISearchMovieResult, ISearchTvResult, RequestType } from "../../../interfaces";
import { SearchV2Service } from "../../../services";
import { StorageService } from "../../../shared/storage/storage-service";
import { MatButtonToggleChange } from '@angular/material/button-toggle';
import { Carousel } from 'primeng/carousel';

export enum DiscoverType {
    Upcoming,
    Trending,
    Popular,
}

@Component({
    selector: "carousel-list",
    templateUrl: "./carousel-list.component.html",
    styleUrls: ["./carousel-list.component.scss"],
})
export class CarouselListComponent implements OnInit {

    @Input() public discoverType: DiscoverType;
    @ViewChild('carousel', {static: false}) carousel: Carousel;

    public DiscoverOption = DiscoverOption;
    public discoverOptions: DiscoverOption = DiscoverOption.Combined;
    public discoverResults: IDiscoverCardResult[] = [];
    public movies: ISearchMovieResult[] = [];
    public tvShows: ISearchTvResult[] = [];
    public responsiveOptions: any;
    public RequestType = RequestType;
    public loadingFlag: boolean;

    get mediaTypeStorageKey() {
        return "DiscoverOptions" + this.discoverType.toString();
    };
    private amountToLoad = 14;
    private currentlyLoaded = 0;

    constructor(private searchService: SearchV2Service,
        private storageService: StorageService) {
        this.responsiveOptions = [
            {
                breakpoint: '2559px',
                numVisible: 7,
                numScroll: 7
            },
            {
                breakpoint: '1024px',
                numVisible: 4,
                numScroll: 4
            },
            {
                breakpoint: '768px',
                numVisible: 2,
                numScroll: 2
            },
            {
                breakpoint: '560px',
                numVisible: 1,
                numScroll: 1
            }
        ];
    }

    public async ngOnInit() {
        this.currentlyLoaded = 0;
        const localDiscoverOptions = +this.storageService.get(this.mediaTypeStorageKey);
        if (localDiscoverOptions) {
            this.discoverOptions = DiscoverOption[DiscoverOption[localDiscoverOptions]];
        }

        var moviePromise: Promise<void>;
        var tvPromise: Promise<void>;
        switch (this.discoverOptions) {
            case DiscoverOption.Combined:
                moviePromise = this.loadMovies();
                tvPromise = this.loadTv();
                break;
            case DiscoverOption.Movie:
                moviePromise = this.loadMovies();
                break;
            case DiscoverOption.Tv:
                tvPromise = this.loadTv();
                break;
        }

        await moviePromise;
        await tvPromise;

        this.createInitialModel();
    }

    public async toggleChanged(event: MatButtonToggleChange) {
        await this.switchDiscoverMode(event.value);
    }

    public async newPage() {
        // Note this is using the internal carousel APIs
        // https://github.com/primefaces/primeng/blob/master/src/app/components/carousel/carousel.ts
        var end = this.carousel._page >= (this.carousel.totalDots() - 1);
        if (end) {
            var moviePromise: Promise<void>;
            var tvPromise: Promise<void>;
            switch (this.discoverOptions) {
                case DiscoverOption.Combined:
                    moviePromise = this.loadMovies();
                    tvPromise = this.loadTv();
                    break;
                case DiscoverOption.Movie:
                    moviePromise = this.loadMovies();
                    break;
                case DiscoverOption.Tv:
                    tvPromise = this.loadTv();
                    break;
            }
            await moviePromise;
            await tvPromise;

            this.createModel();
        }
    }

    private async switchDiscoverMode(newMode: DiscoverOption) {
        if (this.discoverOptions === newMode) {
            return;
        }
        this.loading();
        this.discoverOptions = newMode;
        this.storageService.save(this.mediaTypeStorageKey, newMode.toString());
        await this.ngOnInit();
        this.finishLoading();
    }

    private async loadMovies() {
        switch (this.discoverType) {
            case DiscoverType.Popular:
                this.movies = await this.searchService.popularMoviesByPage(this.currentlyLoaded, this.amountToLoad);
                break;
            case DiscoverType.Trending:
                this.movies = await this.searchService.nowPlayingMoviesByPage(this.currentlyLoaded, this.amountToLoad);
                break;
            case DiscoverType.Upcoming:
                this.movies = await this.searchService.upcomingMoviesByPage(this.currentlyLoaded, this.amountToLoad);
                break
        }
        this.currentlyLoaded += this.amountToLoad;
    }

    private async loadTv() {
        switch (this.discoverType) {
            case DiscoverType.Popular:
                this.tvShows = await this.searchService.popularTvByPage(this.currentlyLoaded, this.amountToLoad);
                break;
            case DiscoverType.Trending:
                this.tvShows = await this.searchService.trendingTvByPage(this.currentlyLoaded, this.amountToLoad);
                break;
            case DiscoverType.Upcoming:
                this.tvShows = await this.searchService.anticipatedTvByPage(this.currentlyLoaded, this.amountToLoad);
                break
        }
        this.currentlyLoaded += this.amountToLoad;
    }

    private createInitialModel() {
        this.clear();
        this.createModel();
    }

    private createModel() {
        const tempResults = <IDiscoverCardResult[]>[];

        switch (this.discoverOptions) {
            case DiscoverOption.Combined:
                tempResults.push(...this.mapMovieModel());
                tempResults.push(...this.mapTvModel());
                this.shuffle(tempResults);
                break;
            case DiscoverOption.Movie:
                tempResults.push(...this.mapMovieModel());
                break;
            case DiscoverOption.Tv:
                tempResults.push(...this.mapTvModel());
                break;
        }

        this.discoverResults.push(...tempResults);
        this.carousel.ngAfterContentInit();

        this.finishLoading();
    }

    private mapMovieModel(): IDiscoverCardResult[] {
        const tempResults = <IDiscoverCardResult[]>[];
        this.movies.forEach(m => {
            tempResults.push({
                available: m.available,
                posterPath: m.posterPath ? `https://image.tmdb.org/t/p/w500/${m.posterPath}` : "../../../images/default_movie_poster.png",
                requested: m.requested,
                title: m.title,
                type: RequestType.movie,
                id: m.id,
                url: `http://www.imdb.com/title/${m.imdbId}/`,
                rating: m.voteAverage,
                overview: m.overview,
                approved: m.approved,
                imdbid: m.imdbId,
                denied: false,
                background: m.backdropPath
            });
        });
        return tempResults;
    }

    private mapTvModel(): IDiscoverCardResult[] {
        const tempResults = <IDiscoverCardResult[]>[];
        this.tvShows.forEach(m => {
            tempResults.push({
                available: m.available,
                posterPath: "../../../images/default_tv_poster.png",
                requested: m.requested,
                title: m.title,
                type: RequestType.tvShow,
                id: m.id,
                url: undefined,
                rating: +m.rating,
                overview: m.overview,
                approved: m.approved || m.partlyAvailable,
                imdbid: m.imdbId,
                denied: false,
                background: m.background
            });
        });
        return tempResults;
    }

    private clear() {
        this.discoverResults = [];
    }

    private shuffle(discover: IDiscoverCardResult[]): IDiscoverCardResult[] {
        for (let i = discover.length - 1; i > 0; i--) {
            const j = Math.floor(Math.random() * (i + 1));
            [discover[i], discover[j]] = [discover[j], discover[i]];
        }
        return discover;
    }

    private loading() {
        this.loadingFlag = true;
    }

    private finishLoading() {
        this.loadingFlag = false;
    }


}
