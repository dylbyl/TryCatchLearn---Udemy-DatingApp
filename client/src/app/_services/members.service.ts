import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { of, pipe } from 'rxjs';
import { map, take } from 'rxjs/operators';
import { environment } from 'src/environments/environment';
import { Member } from '../_models/member';
import { PaginatedResult } from '../_models/pagination';
import { User } from '../_models/user';
import { UserParams } from '../_models/userParams';
import { AccountService } from './account.service';


@Injectable({
	providedIn: 'root'
})
export class MembersService {
	baseUrl = environment.apiUrl;
	members: Member[] = [];
	memberCache = new Map();
	user: User;
	userParams: UserParams;


	constructor(private http: HttpClient, private accountService: AccountService) {
		this.accountService.currentUser$.pipe(take(1)).subscribe(user => {
			this.user = user;
			this.userParams = new UserParams(user);
		})
	}

	getUserParams() {
		return this.userParams;
	}

	setUserParams(params: UserParams) {
		this.userParams = params;
	}

	resetUserParams() {
		this.userParams = new UserParams(this.user);
		return this.userParams;
	}

	getMembers(userParams: UserParams) {
		//Checks our cache for a previous API response that has been stored with this current set of params as a key
		var response = this.memberCache.get(Object.values(userParams).join('-'));
		//If such a response exists in our cache, load it instead of pinging our API (see explanation below!)
		if (response) {
			return of(response);
		}

		let params = this.getPaginationHeaders(userParams.pageNumber, userParams.pageSize);

		params = params.append('minAge', userParams.minAge.toString());
		params = params.append('maxAge', userParams.maxAge.toString());
		params = params.append('gender', userParams.gender);
		params = params.append('orderBy', userParams.orderBy);

		//Complicated part.
		//everything UP TO THE PIPE : fetches results from API using passed in params
		//AFTER THE PIPE : adds a new entry to the memberCache map. (remember, maps are sets of key/value pairs usually)
		// -- this new entry has a key of the params, sent in as a string (so something like minAge-maxAge-pageNumber,-etc)
		// -- the value of this pair is the API response
		// -- this means that, when we load a new result, we can search out map for anything with the current params (key)
		// -- and if we have something in cache that corresponds to this key, we load it from cache instead of API
		// -- this all works the exact same as your coin-saving system in Ginseng (only we're loading data in, instead of deleting coins)
		return this.getPaginatedResult<Member[]>(this.baseUrl + 'users', params)
			.pipe(map(response => {
				this.memberCache.set(Object.values(userParams).join('-'), response);
				return response;
			}))
	}

	getMember(username: string) {
		//Okay this is a lot
		//[...] is the "spread" operator - it automatically includes every element from the array (ie. an array of [1, 2, 3] passed as [...arr] will "spread out" into "1, 2, 3")
		//Here, the spread operator is used to take every .value from the PaginationResult key/value pairs in the memberCache map, and put them into an array alone
		//These values are still objects with Pagination attached, in addition to the User from the API. How do we separate the Users from the Pagination?

		//The answer is .reduce - loops through an array and performs a function on each element in that array.
		//the array is returned into the next loop as the first parameter (in this case "arr")
		//the second parameter is the current element of the loop
		//fat arrow function into the function you'd like to run on these parameters
		//comma after the function, then you can set the INITIAL value of the first array

		//here, we are initializing the first arr value as an empty array
		//then each loop is running .concat on the "result" parameter of each array
		//in this case, it means we're getting the "result" in each PaginationResult element (which will be a single user from our API, without the Pagination info)
		//then .concat adds it to arr, then arr gets passed into the next iteration of the loop

		//Why reduce? It's a simpler way to loop (apparently, although I'm confused as of the time of writing this)
		//It denotes some sort of data transformation. Anything can happen in a loop - only data transformations happen in a reduce
		//Reduce is used to iterate over data and "reduce" it into one thing. 
		//In this case, we're iterating over arrays (multiple PaginationResults), and reducing them into one big array
		//Check this out for more: https://dev.to/babak/why-you-should-use-reduce-instead-of-loops----part-i-5dfa
		//and this: https://codeburst.io/reduce-vs-for-loop-3c1a84e63872

		//And finally! After reduce, we have an array of every member stored in our cache.
		//This includes multiples, which may seem a little inefficient
		//However, this method is for getting ONE member, so that we can view their details page
		//.find will return the first member with the matching username, meaning the rest of the duplicates don't matter
		//if that fails for some reason (ie. a user went to a details page by a URL instead of loading pages into the cache)
		// -- then it skips the If statement and just queries the API

		const member = [...this.memberCache.values()]
			.reduce((arr, elem) => arr.concat(elem.result), [])
			.find((member: Member) => member.userName == username);

		if (member) {
			return of(member);
		}

		return this.http.get<Member>(this.baseUrl + 'users/' + username);
	}

	updateMember(member: Member) {
		return this.http.put(this.baseUrl + 'users', member).pipe(
			map(() => {
				const index = this.members.indexOf(member);
				this.members[index] = member;
			})
		)
	}

	setMainPhoto(photoId: number) {
		return this.http.put(this.baseUrl + 'users/set-main-photo/' + photoId, {});
	}

	deletePhoto(photoId: number) {
		return this.http.delete(this.baseUrl + 'users/delete-photo/' + photoId);
	}

	addLike(username: string) {
		return this.http.post(this.baseUrl + 'likes/' + username, {})
	}

	getLikes(predicate: string, pageNumber, pageSize) {
		let params = this.getPaginationHeaders(pageNumber, pageSize);
		params = params.append('predicate', predicate);
		return this.getPaginatedResult<Partial<Member[]>>(this.baseUrl + 'likes', params);
	}

	private getPaginatedResult<T>(url, params) {
		const paginatedResult: PaginatedResult<T> = new PaginatedResult<T>();

		return this.http.get<T>(url, { observe: 'response', params }).pipe(
			map(response => {
				paginatedResult.result = response.body;
				if (response.headers.get('Pagination') !== null) {
					paginatedResult.pagination = JSON.parse(response.headers.get('Pagination'));
				}
				return paginatedResult;
			})
		);
	}

	private getPaginationHeaders(pageNumber: number, pageSize: number) {
		let params = new HttpParams();

		params = params.append('pageNumber', pageNumber.toString());
		params = params.append('pageSize', pageSize.toString());

		return params;
	}
}
